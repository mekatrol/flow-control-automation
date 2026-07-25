package contracts

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"gopkg.in/yaml.v3"
)

var contractRoot = filepath.Join("..", "..", "..", "..", "testdata", "contracts")

func TestConfigurationYAMLMatchesNormalizedJSON(t *testing.T) {
	tests := []struct {
		yamlFile string
		jsonFile string
		wrapKey  string
	}{
		{"points/v1.yaml", "points/v1.normalized.json", ""},
		{"point-sources/v1.yaml", "point-sources/v1.normalized.json", ""},
		{"controllers/default.v1.yaml", "controllers/default.v1.normalized.json", "templates"},
		{"controllers/constrained.v1.yaml", "controllers/constrained.v1.normalized.json", "templates"},
	}
	for _, test := range tests {
		t.Run(test.yamlFile, func(t *testing.T) {
			yamlValue := decodeStrictFixture(t, test.yamlFile)
			jsonValue := decodeJSONFixture(t, test.jsonFile)
			stripBackendMetadata(jsonValue)
			if test.wrapKey != "" {
				yamlValue = map[string]any{
					"schemaVersion": yamlValue.(map[string]any)["schemaVersion"],
					test.wrapKey:    []any{withoutKey(yamlValue.(map[string]any), "schemaVersion")},
				}
			}
			yamlValue = normalizeJSONValue(yamlValue)
			jsonValue = normalizeJSONValue(jsonValue)
			if !reflect.DeepEqual(yamlValue, jsonValue) {
				t.Fatalf("YAML and normalized JSON differ:\nYAML: %#v\nJSON: %#v", yamlValue, jsonValue)
			}
		})
	}
}

func normalizeJSONValue(value any) any {
	value = stringifyMapKeys(value)
	data, err := json.Marshal(value)
	if err != nil {
		panic(err)
	}
	var normalized any
	if err := json.Unmarshal(data, &normalized); err != nil {
		panic(err)
	}
	return normalized
}

func stringifyMapKeys(value any) any {
	switch typed := value.(type) {
	case map[any]any:
		result := make(map[string]any, len(typed))
		for key, child := range typed {
			result[fmt.Sprint(key)] = stringifyMapKeys(child)
		}
		return result
	case map[string]any:
		for key, child := range typed {
			typed[key] = stringifyMapKeys(child)
		}
	case []any:
		for index, child := range typed {
			typed[index] = stringifyMapKeys(child)
		}
	}
	return value
}

func TestInvalidConfigurationFixturesAreRejected(t *testing.T) {
	for _, name := range []string{
		"points/invalid/unknown-field.yaml",
		"point-sources/invalid/unknown-field.yaml",
		"controllers/invalid/unsupported-schema.yaml",
		"controllers/invalid/alias.yaml",
		"controllers/invalid/syntax.yaml",
		"controllers/invalid/unknown-field.yaml",
	} {
		t.Run(name, func(t *testing.T) {
			data, err := os.ReadFile(filepath.Join(contractRoot, name))
			if err != nil {
				t.Fatal(err)
			}
			if _, err := parseStrictYAML(data, name); err == nil {
				t.Fatal("invalid fixture was accepted")
			}
		})
	}
}

func decodeStrictFixture(t *testing.T, name string) any {
	t.Helper()
	data, err := os.ReadFile(filepath.Join(contractRoot, name))
	if err != nil {
		t.Fatal(err)
	}
	value, err := parseStrictYAML(data, name)
	if err != nil {
		t.Fatal(err)
	}
	return value
}

func parseStrictYAML(data []byte, name string) (any, error) {
	var document yaml.Node
	decoder := yaml.NewDecoder(strings.NewReader(string(data)))
	if err := decoder.Decode(&document); err != nil {
		return nil, err
	}
	if document.Content == nil {
		return nil, fmt.Errorf("empty YAML")
	}
	if err := validateNode(document.Content[0]); err != nil {
		return nil, err
	}
	var value any
	if err := document.Content[0].Decode(&value); err != nil {
		return nil, err
	}
	root, ok := value.(map[string]any)
	if !ok || root["schemaVersion"] != 1 {
		return nil, fmt.Errorf("schemaVersion must be 1")
	}
	if strings.Contains(name, "points/") {
		if err := rejectUnknownItemFields(root["points"], pointFields()); err != nil {
			return nil, err
		}
	}
	if strings.Contains(name, "point-sources/") {
		if err := rejectUnknownItemFields(root["sources"], sourceFields()); err != nil {
			return nil, err
		}
	}
	if strings.Contains(name, "controllers/") {
		for field := range root {
			if !controllerFields()[field] {
				return nil, fmt.Errorf("unknown field %q", field)
			}
		}
	}
	return value, nil
}

func validateNode(node *yaml.Node) error {
	if node.Kind == yaml.AliasNode || node.Anchor != "" {
		return fmt.Errorf("YAML aliases and anchors are unsupported")
	}
	if node.Tag != "" && !strings.HasPrefix(node.Tag, "!!") {
		return fmt.Errorf("custom YAML tags are unsupported")
	}
	if node.Kind == yaml.MappingNode {
		seen := map[string]bool{}
		for index := 0; index < len(node.Content); index += 2 {
			key := node.Content[index].Value
			if seen[key] {
				return fmt.Errorf("duplicate key %q", key)
			}
			seen[key] = true
		}
	}
	for _, child := range node.Content {
		if err := validateNode(child); err != nil {
			return err
		}
	}
	return nil
}

func rejectUnknownItemFields(value any, allowed map[string]bool) error {
	items, ok := value.([]any)
	if !ok {
		return nil
	}
	for _, item := range items {
		for key := range item.(map[string]any) {
			if !allowed[key] {
				return fmt.Errorf("unknown field %q", key)
			}
		}
	}
	return nil
}

func pointFields() map[string]bool {
	result := map[string]bool{}
	for _, field := range []string{
		"id", "name", "description", "enabled", "groupId", "implementation", "direction",
		"valueType", "units", "stateLabels", "readable", "commandable", "persistence",
		"relinquishDefault", "sourceId", "mapping", "limits", "safeDisablePolicy",
	} {
		result[field] = true
	}
	return result
}

func sourceFields() map[string]bool {
	result := map[string]bool{}
	for _, field := range []string{
		"id", "name", "description", "enabled", "kind", "connection", "credentialRef", "tls", "timeouts",
	} {
		result[field] = true
	}
	return result
}

func controllerFields() map[string]bool {
	result := map[string]bool{}
	for _, field := range []string{
		"schemaVersion", "id", "name", "description", "readOnly", "capabilities", "limits",
	} {
		result[field] = true
	}
	return result
}

func decodeJSONFixture(t *testing.T, name string) any {
	t.Helper()
	data, err := os.ReadFile(filepath.Join(contractRoot, name))
	if err != nil {
		t.Fatal(err)
	}
	var value any
	if err := json.Unmarshal(data, &value); err != nil {
		t.Fatal(err)
	}
	return value
}

func stripBackendMetadata(value any) {
	switch typed := value.(type) {
	case map[string]any:
		delete(typed, "revision")
		delete(typed, "createdAt")
		delete(typed, "updatedAt")
		for _, child := range typed {
			stripBackendMetadata(child)
		}
	case []any:
		for _, child := range typed {
			stripBackendMetadata(child)
		}
	}
}

func withoutKey(source map[string]any, key string) map[string]any {
	result := make(map[string]any, len(source)-1)
	for field, value := range source {
		if field != key {
			result[field] = value
		}
	}
	return result
}
