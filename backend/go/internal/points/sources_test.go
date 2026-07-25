package points

import (
	"context"
	"errors"
	"net"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

func TestSourceFixturesRoundTrip(t *testing.T) {
	data, err := os.ReadFile(filepath.Join("..", "..", "..", "..", "testdata", "contracts", "point-sources", "v1.yaml"))
	if err != nil {
		t.Fatal(err)
	}
	document, err := ParseSourcesYAML(data)
	if err != nil {
		t.Fatal(err)
	}
	if len(document.Sources) != 3 {
		t.Fatalf("got %d sources", len(document.Sources))
	}
	for _, source := range document.Sources {
		rendered, err := RenderSourceYAML(source)
		if err != nil {
			t.Fatal(err)
		}
		roundTrip, err := ParseSourceYAML(rendered)
		if err != nil {
			t.Fatal(err)
		}
		if roundTrip.ID != source.ID || roundTrip.Kind != source.Kind {
			t.Fatalf("round trip changed source: %#v", roundTrip)
		}
	}
}

func TestStrictYAMLAndSourceValidation(t *testing.T) {
	valid := `schemaVersion: 1
sources:
  - id: weather
    name: Weather
    enabled: true
    kind: http_json
    connection:
      baseUrl: https://example.test
      allowedReadMethods: [GET]
      maximumResponseBytes: 1024
    tls: {verifyServerCertificate: true}
    timeouts: {connectMilliseconds: 100, requestMilliseconds: 100}
`
	tests := map[string]string{
		"duplicate key": strings.Replace(valid, "name: Weather", "name: Weather\n    name: Duplicate", 1),
		"alias":         strings.Replace(valid, "connection:", "connection: &connection", 1),
		"unknown":       strings.Replace(valid, "enabled: true", "enabled: true\n    password: secret", 1),
		"literal auth":  strings.Replace(valid, "kind: http_json", "kind: http_json\n    credentialRef: password", 1),
		"unsafe method": strings.Replace(valid, "[GET]", "[POST]", 1),
		"insecure url":  strings.Replace(valid, "https://", "http://", 1),
	}
	for name, input := range tests {
		t.Run(name, func(t *testing.T) {
			if _, err := ParseSourceYAML([]byte(input)); err == nil {
				t.Fatal("invalid source accepted")
			}
		})
	}
}

func TestStoreRevisionRollbackAndRestart(t *testing.T) {
	path := filepath.Join(t.TempDir(), "point-sources.json")
	store, err := OpenSourceStore(path)
	if err != nil {
		t.Fatal(err)
	}
	source := validHTTPSource()
	created, err := store.Create(source)
	if err != nil {
		t.Fatal(err)
	}
	source.Name, source.Revision = "Updated", created.Revision
	updated, err := store.Update(source.ID, source)
	if err != nil {
		t.Fatal(err)
	}
	source.Revision = created.Revision
	if _, err := store.Update(source.ID, source); !errors.Is(err, ErrConflict) {
		t.Fatalf("expected stale revision conflict, got %v", err)
	}
	reopened, err := OpenSourceStore(path)
	if err != nil {
		t.Fatal(err)
	}
	got, err := reopened.Get(source.ID)
	if err != nil || got.Revision != updated.Revision || got.Name != "Updated" {
		t.Fatalf("restart changed source: %#v, %v", got, err)
	}
}

func TestConnectivityRejectsPrivateAddressesAndRateLimits(t *testing.T) {
	tester := NewTester(EnvironmentCredentials{})
	tester.Lookup = func(context.Context, string) ([]net.IPAddr, error) {
		return []net.IPAddr{{IP: net.ParseIP("127.0.0.1")}}, nil
	}
	result := tester.Test(context.Background(), validHTTPSource(), "client")
	if result.Stages[len(result.Stages)-1].Name != "dns" ||
		!strings.Contains(result.Stages[len(result.Stages)-1].Diagnostic, "forbidden") {
		t.Fatalf("unexpected result: %#v", result)
	}
	for index := 0; index < 10; index++ {
		tester.Test(context.Background(), validHTTPSource(), "limited")
	}
	result = tester.Test(context.Background(), validHTTPSource(), "limited")
	if result.Stages[0].Name != "policy" {
		t.Fatalf("rate limit not enforced: %#v", result)
	}
}

func TestConnectivityAllowsPrivateAddressOnlyWithExplicitOptIn(t *testing.T) {
	tester := NewTester(EnvironmentCredentials{})
	tester.Lookup = func(context.Context, string) ([]net.IPAddr, error) {
		return []net.IPAddr{{IP: net.ParseIP("192.168.1.20")}}, nil
	}
	dialled := false
	tester.Dial = func(context.Context, string, string) (net.Conn, error) {
		dialled = true
		return nil, errors.New("expected test stop")
	}
	source := validHTTPSource()
	source.Connection.AllowPrivateNetwork = true
	result := tester.Test(context.Background(), source, "private-opt-in")
	if !dialled || result.Stages[0].Status != "passed" || result.Stages[0].Name != "dns" {
		t.Fatalf("private destination did not pass DNS policy: %#v", result)
	}
	if !isForbiddenIP(net.ParseIP("127.0.0.1"), true) {
		t.Fatal("loopback must remain forbidden when private networks are allowed")
	}
}

func TestMQTTTestAuthenticatesAndSubscribesToExactTopic(t *testing.T) {
	client, broker := net.Pipe()
	defer client.Close()
	defer broker.Close()
	qos := 1
	source := validHTTPSource()
	source.Kind = "mqtt"
	source.Connection = Connection{
		BrokerURL: "mqtts://mqtt.example.test:8883", ClientIDPrefix: "test",
		TestTopic: "plant/temperature", QOS: &qos,
	}
	go func() {
		buffer := make([]byte, 512)
		_, _ = broker.Read(buffer)
		_, _ = broker.Write([]byte{0x20, 0x02, 0x00, 0x00})
		count, _ := broker.Read(buffer)
		if !strings.Contains(string(buffer[:count]), source.Connection.TestTopic) {
			return
		}
		_, _ = broker.Write([]byte{0x90, 0x03, 0x00, 0x01, 0x01})
	}()
	_ = client.SetDeadline(time.Now().Add(time.Second))
	if err := testMQTT(client, source, `{"username":"reader","password":"secret"}`); err != nil {
		t.Fatal(err)
	}
}

func TestMQTTRejectsUnstructuredCredentialsAndWildcardTestTopic(t *testing.T) {
	source := validHTTPSource()
	qos := 0
	source.Kind = "mqtt"
	source.Connection = Connection{BrokerURL: "mqtts://mqtt.example.test:8883", QOS: &qos, TestTopic: "plant/#"}
	if err := source.Validate(); err == nil || !strings.Contains(err.Error(), "without wildcards") {
		t.Fatalf("expected topic validation error, got %v", err)
	}
	client, broker := net.Pipe()
	defer client.Close()
	defer broker.Close()
	source.Connection.TestTopic = ""
	if err := testMQTT(client, source, "reader:secret"); err == nil ||
		!strings.Contains(err.Error(), "JSON") {
		t.Fatalf("expected credential format error, got %v", err)
	}
}

func validHTTPSource() Source {
	follow := false
	return Source{
		ID: "weather", Name: "Weather", Enabled: true, Kind: "http_json",
		Connection: Connection{
			BaseURL: "https://example.test", AllowedReadMethods: []string{"GET"},
			FollowRedirects: &follow, MaximumResponseBytes: 1024,
		},
		TLS:      TLSOptions{VerifyServerCertificate: true},
		Timeouts: Timeouts{ConnectMilliseconds: 100, RequestMilliseconds: 100},
	}
}
