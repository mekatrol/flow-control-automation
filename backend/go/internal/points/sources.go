package points

import (
	"bytes"
	"errors"
	"fmt"
	"io"
	"net"
	"net/url"
	"regexp"
	"strings"

	"gopkg.in/yaml.v3"
)

const (
	SchemaVersion = 1
	MaxYAMLBytes  = 256 << 10
)

var (
	ErrNotFound = errors.New("point source not found")
	ErrConflict = errors.New("point source conflict")
	validID     = regexp.MustCompile(`^[a-z0-9]+(?:-[a-z0-9]+)*$`)
)

type TLSOptions struct {
	VerifyServerCertificate bool `json:"verifyServerCertificate" yaml:"verifyServerCertificate"`
}

type Timeouts struct {
	ConnectMilliseconds int `json:"connectMilliseconds,omitempty" yaml:"connectMilliseconds,omitempty"`
	RequestMilliseconds int `json:"requestMilliseconds,omitempty" yaml:"requestMilliseconds,omitempty"`
}

type Connection struct {
	BaseURL                 string   `json:"baseUrl,omitempty" yaml:"baseUrl,omitempty"`
	SubscribeEvents         *bool    `json:"subscribeEvents,omitempty" yaml:"subscribeEvents,omitempty"`
	BrokerURL               string   `json:"brokerUrl,omitempty" yaml:"brokerUrl,omitempty"`
	ClientIDPrefix          string   `json:"clientIdPrefix,omitempty" yaml:"clientIdPrefix,omitempty"`
	TestTopic               string   `json:"testTopic,omitempty" yaml:"testTopic,omitempty"`
	QOS                     *int     `json:"qos,omitempty" yaml:"qos,omitempty"`
	CleanStart              *bool    `json:"cleanStart,omitempty" yaml:"cleanStart,omitempty"`
	KeepAliveSeconds        int      `json:"keepAliveSeconds,omitempty" yaml:"keepAliveSeconds,omitempty"`
	AllowedReadMethods      []string `json:"allowedReadMethods,omitempty" yaml:"allowedReadMethods,omitempty"`
	DefaultPollMilliseconds int      `json:"defaultPollMilliseconds,omitempty" yaml:"defaultPollMilliseconds,omitempty"`
	FollowRedirects         *bool    `json:"followRedirects,omitempty" yaml:"followRedirects,omitempty"`
	MaximumResponseBytes    int64    `json:"maximumResponseBytes,omitempty" yaml:"maximumResponseBytes,omitempty"`
	AllowPrivateNetwork     bool     `json:"allowPrivateNetwork,omitempty" yaml:"allowPrivateNetwork,omitempty"`
}

type Source struct {
	ID            string     `json:"id" yaml:"id"`
	Name          string     `json:"name" yaml:"name"`
	Description   string     `json:"description,omitempty" yaml:"description,omitempty"`
	Enabled       bool       `json:"enabled" yaml:"enabled"`
	Kind          string     `json:"kind" yaml:"kind"`
	Connection    Connection `json:"connection" yaml:"connection"`
	CredentialRef string     `json:"credentialRef,omitempty" yaml:"credentialRef,omitempty"`
	TLS           TLSOptions `json:"tls" yaml:"tls"`
	Timeouts      Timeouts   `json:"timeouts" yaml:"timeouts"`
	Revision      int        `json:"revision,omitempty" yaml:"-"`
	CreatedAt     string     `json:"createdAt,omitempty" yaml:"-"`
	UpdatedAt     string     `json:"updatedAt,omitempty" yaml:"-"`
}

type YAMLDocument struct {
	SchemaVersion int      `yaml:"schemaVersion"`
	Sources       []Source `yaml:"sources"`
}

func ParseSourceYAML(data []byte) (Source, error) {
	document, err := ParseSourcesYAML(data)
	if err != nil {
		return Source{}, err
	}
	if len(document.Sources) != 1 {
		return Source{}, errors.New("YAML must contain exactly one source")
	}
	return document.Sources[0], nil
}

func ParseSourcesYAML(data []byte) (YAMLDocument, error) {
	if len(data) > MaxYAMLBytes {
		return YAMLDocument{}, errors.New("YAML exceeds 256 KiB limit")
	}
	var syntax yaml.Node
	decoder := yaml.NewDecoder(bytes.NewReader(data))
	if err := decoder.Decode(&syntax); err != nil {
		return YAMLDocument{}, fmt.Errorf("invalid YAML: %w", err)
	}
	if len(syntax.Content) == 0 {
		return YAMLDocument{}, errors.New("YAML document is empty")
	}
	if err := validateYAMLNode(syntax.Content[0], 0); err != nil {
		return YAMLDocument{}, err
	}
	decoder = yaml.NewDecoder(bytes.NewReader(data))
	decoder.KnownFields(true)
	var document YAMLDocument
	if err := decoder.Decode(&document); err != nil {
		return YAMLDocument{}, fmt.Errorf("invalid source configuration: %w", err)
	}
	var extra any
	if err := decoder.Decode(&extra); !errors.Is(err, io.EOF) {
		return YAMLDocument{}, errors.New("YAML must contain one document")
	}
	if document.SchemaVersion != SchemaVersion {
		return YAMLDocument{}, errors.New("schemaVersion must be 1")
	}
	names, ids := map[string]bool{}, map[string]bool{}
	for index := range document.Sources {
		if err := document.Sources[index].Validate(); err != nil {
			return YAMLDocument{}, fmt.Errorf("sources[%d]: %w", index, err)
		}
		name := strings.ToLower(document.Sources[index].Name)
		if names[name] || ids[document.Sources[index].ID] {
			return YAMLDocument{}, errors.New("source IDs and names must be unique")
		}
		names[name], ids[document.Sources[index].ID] = true, true
	}
	return document, nil
}

func validateYAMLNode(node *yaml.Node, depth int) error {
	if depth > 20 {
		return errors.New("YAML nesting exceeds 20 levels")
	}
	if node.Kind == yaml.AliasNode || node.Anchor != "" {
		return errors.New("YAML aliases and anchors are unsupported")
	}
	if node.Tag != "" && !strings.HasPrefix(node.Tag, "!!") {
		return errors.New("custom YAML tags are unsupported")
	}
	if node.Kind == yaml.MappingNode {
		seen := map[string]bool{}
		for index := 0; index < len(node.Content); index += 2 {
			key := node.Content[index].Value
			if seen[key] {
				return fmt.Errorf("duplicate YAML key %q", key)
			}
			seen[key] = true
		}
	}
	for _, child := range node.Content {
		if err := validateYAMLNode(child, depth+1); err != nil {
			return err
		}
	}
	return nil
}

func (source Source) Validate() error {
	source.ID, source.Name = strings.TrimSpace(source.ID), strings.TrimSpace(source.Name)
	if !validID.MatchString(source.ID) {
		return errors.New("id must be a lowercase hyphenated identifier")
	}
	if source.Name == "" {
		return errors.New("name must be non-empty")
	}
	if source.CredentialRef != "" && !strings.HasPrefix(source.CredentialRef, "env:") && !strings.HasPrefix(source.CredentialRef, "secret://") {
		return errors.New("credentialRef must use env: or secret://")
	}
	if source.Timeouts.ConnectMilliseconds < 100 || source.Timeouts.ConnectMilliseconds > 30000 {
		return errors.New("timeouts.connectMilliseconds must be between 100 and 30000")
	}
	if source.Timeouts.RequestMilliseconds != 0 && (source.Timeouts.RequestMilliseconds < 100 || source.Timeouts.RequestMilliseconds > 60000) {
		return errors.New("timeouts.requestMilliseconds must be between 100 and 60000 when set")
	}
	switch source.Kind {
	case "home_assistant":
		if source.Connection.BaseURL == "" {
			return errors.New("connection.baseUrl is required")
		}
	case "http_json":
		if source.Connection.BaseURL == "" {
			return errors.New("connection.baseUrl is required")
		}
		for _, method := range source.Connection.AllowedReadMethods {
			if method != "GET" && method != "HEAD" {
				return errors.New("only GET and HEAD are allowed read methods")
			}
		}
		if source.Connection.MaximumResponseBytes < 1 || source.Connection.MaximumResponseBytes > 10<<20 {
			return errors.New("maximumResponseBytes must be between 1 and 10485760")
		}
	case "mqtt":
		if source.Connection.BrokerURL == "" {
			return errors.New("connection.brokerUrl is required")
		}
		if source.Connection.QOS == nil || *source.Connection.QOS < 0 || *source.Connection.QOS > 2 {
			return errors.New("connection.qos must be 0, 1, or 2")
		}
		if topic := source.Connection.TestTopic; strings.ContainsAny(topic, "+#\x00") || len(topic) > 65535 {
			return errors.New("connection.testTopic must be an exact MQTT topic without wildcards")
		}
	default:
		return errors.New("kind must be home_assistant, mqtt, or http_json")
	}
	address := source.Connection.BaseURL
	if source.Kind == "mqtt" {
		address = source.Connection.BrokerURL
	}
	parsed, err := url.Parse(address)
	if err != nil || parsed.Hostname() == "" || parsed.User != nil {
		return errors.New("connection URL must be absolute and must not contain credentials")
	}
	allowed := map[string]bool{"https": true}
	if source.Kind == "mqtt" {
		allowed = map[string]bool{"mqtt": true, "mqtts": true}
	}
	if !allowed[parsed.Scheme] {
		return errors.New("connection URL scheme is not allowed")
	}
	if !source.TLS.VerifyServerCertificate && (parsed.Scheme == "https" || parsed.Scheme == "mqtts") {
		return errors.New("TLS server certificate verification must be enabled")
	}
	return nil
}

func RenderSourceYAML(source Source) ([]byte, error) {
	source.Revision, source.CreatedAt, source.UpdatedAt = 0, "", ""
	return yaml.Marshal(YAMLDocument{SchemaVersion: SchemaVersion, Sources: []Source{source}})
}

func isForbiddenIP(ip net.IP, allowPrivateNetwork bool) bool {
	if ip.IsLoopback() || ip.IsLinkLocalMulticast() || ip.IsLinkLocalUnicast() || ip.IsUnspecified() {
		return true
	}
	return ip.IsPrivate() && !allowPrivateNetwork
}
