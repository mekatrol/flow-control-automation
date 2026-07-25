package points

import (
	"context"
	"crypto/tls"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"os"
	"strconv"
	"strings"
	"sync"
	"time"
)

type CredentialResolver interface {
	Resolve(context.Context, string) (string, error)
}

type EnvironmentCredentials struct{}

func (EnvironmentCredentials) Resolve(_ context.Context, reference string) (string, error) {
	if reference == "" {
		return "", nil
	}
	if !strings.HasPrefix(reference, "env:") {
		return "", errors.New("credential reference is unavailable in this deployment")
	}
	value, ok := os.LookupEnv(strings.TrimPrefix(reference, "env:"))
	if !ok {
		return "", errors.New("referenced credential is unavailable")
	}
	return value, nil
}

type TestStage struct {
	Name       string `json:"name"`
	Status     string `json:"status"`
	Diagnostic string `json:"diagnostic,omitempty"`
}

type TestResult struct {
	Status   string      `json:"status"`
	Duration int64       `json:"durationMilliseconds"`
	Stages   []TestStage `json:"stages"`
}

type Tester struct {
	Resolver CredentialResolver
	Lookup   func(context.Context, string) ([]net.IPAddr, error)
	Dial     func(context.Context, string, string) (net.Conn, error)
	mu       sync.Mutex
	recent   map[string][]time.Time
}

func NewTester(resolver CredentialResolver) *Tester {
	dialer := &net.Dialer{}
	return &Tester{
		Resolver: resolver,
		Lookup:   net.DefaultResolver.LookupIPAddr,
		Dial:     dialer.DialContext,
		recent:   map[string][]time.Time{},
	}
}

func (tester *Tester) Test(ctx context.Context, source Source, clientKey string) (result TestResult) {
	started := time.Now()
	result = TestResult{Status: "failed", Stages: []TestStage{}}
	defer func() { result.Duration = time.Since(started).Milliseconds() }()
	if !tester.allow(clientKey, started) {
		result.Stages = append(result.Stages, TestStage{Name: "policy", Status: "failed", Diagnostic: "connection test rate limit exceeded"})
		return result
	}
	if err := source.Validate(); err != nil {
		result.Stages = append(result.Stages, TestStage{Name: "validation", Status: "failed", Diagnostic: err.Error()})
		return result
	}
	rawURL := source.Connection.BaseURL
	if source.Kind == "mqtt" {
		rawURL = source.Connection.BrokerURL
	}
	target, _ := url.Parse(rawURL)
	addresses, err := tester.Lookup(ctx, target.Hostname())
	if err != nil {
		return failedStage(result, "dns", "host lookup failed")
	}
	for _, address := range addresses {
		if isForbiddenIP(address.IP, source.Connection.AllowPrivateNetwork) {
			return failedStage(result, "dns", "destination address is forbidden by outbound network policy")
		}
	}
	result.Stages = append(result.Stages, TestStage{Name: "dns", Status: "passed"})
	port := target.Port()
	if port == "" {
		if target.Scheme == "https" || target.Scheme == "mqtts" {
			port = "443"
			if target.Scheme == "mqtts" {
				port = "8883"
			}
		} else {
			port = "1883"
		}
	}
	timeout := time.Duration(source.Timeouts.ConnectMilliseconds) * time.Millisecond
	connectCtx, cancel := context.WithTimeout(ctx, timeout)
	defer cancel()
	connection, err := tester.Dial(connectCtx, "tcp", net.JoinHostPort(target.Hostname(), port))
	if err != nil {
		if errors.Is(connectCtx.Err(), context.Canceled) {
			return failedStage(result, "tcp", "connection test cancelled")
		}
		return failedStage(result, "tcp", "TCP connection failed")
	}
	defer connection.Close()
	result.Stages = append(result.Stages, TestStage{Name: "tcp", Status: "passed"})

	if target.Scheme == "https" || target.Scheme == "mqtts" {
		tlsConnection := tls.Client(connection, &tls.Config{ServerName: target.Hostname(), MinVersion: tls.VersionTLS12})
		if err := tlsConnection.HandshakeContext(connectCtx); err != nil {
			return failedStage(result, "tls", "TLS handshake or certificate verification failed")
		}
		connection = tlsConnection
		result.Stages = append(result.Stages, TestStage{Name: "tls", Status: "passed"})
	}
	credential, err := tester.Resolver.Resolve(ctx, source.CredentialRef)
	if err != nil {
		return failedStage(result, "authentication", err.Error())
	}
	result.Stages = append(result.Stages, TestStage{Name: "authentication", Status: "passed"})
	if source.Kind == "mqtt" {
		protocolTimeout := source.Timeouts.RequestMilliseconds
		if protocolTimeout == 0 {
			protocolTimeout = source.Timeouts.ConnectMilliseconds
		}
		_ = connection.SetDeadline(time.Now().Add(time.Duration(protocolTimeout) * time.Millisecond))
		if err := testMQTT(connection, source, credential); err != nil {
			return failedStage(result, "protocol", err.Error())
		}
	} else if err := tester.testHTTP(ctx, source, target, credential, addresses); err != nil {
		return failedStage(result, "protocol", err.Error())
	}
	result.Stages = append(result.Stages, TestStage{Name: "protocol", Status: "passed"})
	result.Status = "passed"
	return result
}

func failedStage(result TestResult, name, diagnostic string) TestResult {
	result.Stages = append(result.Stages, TestStage{Name: name, Status: "failed", Diagnostic: diagnostic})
	return result
}

func (tester *Tester) allow(key string, now time.Time) bool {
	tester.mu.Lock()
	defer tester.mu.Unlock()
	cutoff := now.Add(-time.Minute)
	current := tester.recent[key][:0]
	for _, item := range tester.recent[key] {
		if item.After(cutoff) {
			current = append(current, item)
		}
	}
	if len(current) >= 10 {
		tester.recent[key] = current
		return false
	}
	tester.recent[key] = append(current, now)
	return true
}

func (tester *Tester) testHTTP(ctx context.Context, source Source, target *url.URL, credential string, resolved []net.IPAddr) error {
	requestTimeout := source.Timeouts.RequestMilliseconds
	if requestTimeout == 0 {
		requestTimeout = source.Timeouts.ConnectMilliseconds
	}
	transport := &http.Transport{
		Proxy: nil,
		DialContext: func(ctx context.Context, network, _ string) (net.Conn, error) {
			port := target.Port()
			if port == "" {
				port = "443"
			}
			return tester.Dial(ctx, network, net.JoinHostPort(resolved[0].IP.String(), port))
		},
		TLSClientConfig: &tls.Config{ServerName: target.Hostname(), MinVersion: tls.VersionTLS12},
	}
	client := &http.Client{Transport: transport, Timeout: time.Duration(requestTimeout) * time.Millisecond}
	client.CheckRedirect = func(request *http.Request, via []*http.Request) error {
		if source.Connection.FollowRedirects == nil || !*source.Connection.FollowRedirects {
			return http.ErrUseLastResponse
		}
		addresses, err := tester.Lookup(request.Context(), request.URL.Hostname())
		if err != nil {
			return errors.New("redirect host lookup failed")
		}
		for _, address := range addresses {
			if isForbiddenIP(address.IP, source.Connection.AllowPrivateNetwork) {
				return errors.New("redirect destination is forbidden")
			}
		}
		if len(via) >= 3 {
			return errors.New("too many redirects")
		}
		return nil
	}
	endpoint := *target
	if source.Kind == "home_assistant" {
		endpoint.Path = strings.TrimRight(endpoint.Path, "/") + "/api/"
	}
	request, _ := http.NewRequestWithContext(ctx, http.MethodGet, endpoint.String(), nil)
	if credential != "" {
		request.Header.Set("Authorization", "Bearer "+credential)
	}
	response, err := client.Do(request)
	if err != nil {
		if errors.Is(ctx.Err(), context.Canceled) {
			return errors.New("connection test cancelled")
		}
		return errors.New("HTTP protocol check failed")
	}
	defer response.Body.Close()
	limit := source.Connection.MaximumResponseBytes
	if limit == 0 {
		limit = 64 << 10
	}
	body, err := io.ReadAll(io.LimitReader(response.Body, limit+1))
	if err != nil {
		return errors.New("HTTP response could not be read")
	}
	if int64(len(body)) > limit {
		return errors.New("HTTP response exceeded the configured size limit")
	}
	if response.StatusCode == http.StatusUnauthorized || response.StatusCode == http.StatusForbidden {
		return errors.New("authentication was rejected")
	}
	if response.StatusCode >= 400 {
		return fmt.Errorf("HTTP protocol check returned status %d", response.StatusCode)
	}
	return nil
}

func testMQTT(connection net.Conn, source Source, credential string) error {
	clientID := source.Connection.ClientIDPrefix + "-test-" + strconv.FormatInt(time.Now().UnixNano(), 36)
	payload := append([]byte{0, 4, 'M', 'Q', 'T', 'T', 4, 2, 0, 10}, mqttString([]byte(clientID))...)
	if credential != "" {
		var login struct {
			Username string `json:"username"`
			Password string `json:"password"`
		}
		if err := json.Unmarshal([]byte(credential), &login); err != nil || login.Username == "" {
			return errors.New("MQTT credential must be JSON with username and password")
		}
		payload[7] |= 0xc0
		payload = append(payload, mqttString([]byte(login.Username))...)
		payload = append(payload, mqttString([]byte(login.Password))...)
	}
	packet := mqttPacket(0x10, payload)
	if _, err := connection.Write(packet); err != nil {
		return err
	}
	reply := make([]byte, 4)
	if _, err := io.ReadFull(connection, reply); err != nil {
		return err
	}
	if reply[0] != 0x20 || reply[3] != 0 {
		return errors.New("MQTT CONNACK rejected")
	}
	if source.Connection.TestTopic != "" {
		subscribe := []byte{0, 1}
		subscribe = append(subscribe, mqttString([]byte(source.Connection.TestTopic))...)
		subscribe = append(subscribe, byte(*source.Connection.QOS))
		if _, err := connection.Write(mqttPacket(0x82, subscribe)); err != nil {
			return err
		}
		header := make([]byte, 2)
		if _, err := io.ReadFull(connection, header); err != nil {
			return err
		}
		if header[0] != 0x90 || header[1] < 3 {
			return errors.New("invalid MQTT SUBACK")
		}
		suback := make([]byte, int(header[1]))
		if _, err := io.ReadFull(connection, suback); err != nil {
			return err
		}
		if suback[len(suback)-1] == 0x80 {
			return errors.New("MQTT topic subscription rejected")
		}
	}
	_, _ = connection.Write([]byte{0xe0, 0})
	return nil
}

func mqttPacket(packetType byte, payload []byte) []byte {
	packet := []byte{packetType}
	remaining := len(payload)
	for {
		encoded := byte(remaining % 128)
		remaining /= 128
		if remaining > 0 {
			encoded |= 0x80
		}
		packet = append(packet, encoded)
		if remaining == 0 {
			break
		}
	}
	return append(packet, payload...)
}

func mqttString(value []byte) []byte {
	result := make([]byte, len(value)+2)
	binary.BigEndian.PutUint16(result, uint16(len(value)))
	copy(result[2:], value)
	return result
}
