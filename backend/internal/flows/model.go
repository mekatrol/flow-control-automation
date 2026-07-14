package flows

import (
	"errors"
	"fmt"
	"math"
	"strings"
	"time"
)

type Flow struct {
	ID          string       `json:"id"`
	Name        string       `json:"name"`
	Description string       `json:"description"`
	Status      string       `json:"status"`
	UpdatedAt   string       `json:"updatedAt"`
	Nodes       []Node       `json:"nodes"`
	Connections []Connection `json:"connections"`
}

type Node struct {
	ID            string         `json:"id"`
	Kind          string         `json:"kind"`
	Label         string         `json:"label"`
	X             float64        `json:"x"`
	Y             float64        `json:"y"`
	ZOrder        float64        `json:"zOrder"`
	Connectors    []Connector    `json:"connectors"`
	Configuration map[string]any `json:"configuration"`
}

type Connector struct {
	ID        string `json:"id"`
	Label     string `json:"label"`
	Direction string `json:"direction"`
	DataType  string `json:"dataType"`
	Side      string `json:"side"`
}

type Endpoint struct {
	NodeID      string `json:"nodeId"`
	ConnectorID string `json:"connectorId"`
}

type Connection struct {
	ID    string   `json:"id"`
	Start Endpoint `json:"start"`
	End   Endpoint `json:"end"`
}

var (
	// Keep this wire-level catalogue aligned with the node kinds emitted by the
	// browser. Execution support can vary by kind, but every graph the editor can
	// construct must remain saveable.
	validKinds = values(
		"and", "average", "calculator", "calendar", "clamp", "comparator",
		"delay", "if", "invert", "line", "max", "min", "nand", "nor", "not", "or", "override",
		"pulse", "schedule", "selector", "sequence", "split", "timer", "xnor", "xor",
	)
	validStatuses   = values("draft", "deployed")
	validDirections = values("input", "output")
	validDataTypes  = values("any", "boolean", "event", "number", "string")
	validSides      = values("left", "right", "top", "bottom")
)

func values(entries ...string) map[string]bool {
	result := make(map[string]bool, len(entries))
	for _, entry := range entries {
		result[entry] = true
	}
	return result
}

// Validate mirrors the browser DTO boundary so invalid graphs never become the
// durable source of truth merely because they reached the server directly.
func (flow Flow) Validate() error {
	if strings.TrimSpace(flow.ID) == "" || strings.TrimSpace(flow.Name) == "" {
		return errors.New("id and name must be non-empty")
	}
	if !validStatuses[flow.Status] {
		return fmt.Errorf("unsupported flow status %q", flow.Status)
	}
	if _, err := time.Parse(time.RFC3339, flow.UpdatedAt); err != nil {
		return errors.New("updatedAt must be an RFC 3339 date-time")
	}

	nodes := make(map[string]map[string]Connector, len(flow.Nodes))
	for nodeIndex, node := range flow.Nodes {
		if strings.TrimSpace(node.ID) == "" || strings.TrimSpace(node.Label) == "" {
			return fmt.Errorf("nodes[%d]: id and label must be non-empty", nodeIndex)
		}
		if _, duplicate := nodes[node.ID]; duplicate {
			return fmt.Errorf("nodes: duplicate id %q", node.ID)
		}
		if !validKinds[node.Kind] {
			return fmt.Errorf("nodes[%d]: unsupported kind", nodeIndex)
		}
		if math.IsNaN(node.X) || math.IsInf(node.X, 0) || math.IsNaN(node.Y) || math.IsInf(node.Y, 0) || math.IsNaN(node.ZOrder) || math.IsInf(node.ZOrder, 0) {
			return fmt.Errorf("nodes[%d]: coordinates and zOrder must be finite", nodeIndex)
		}

		connectors := make(map[string]Connector, len(node.Connectors))
		for connectorIndex, connector := range node.Connectors {
			if strings.TrimSpace(connector.ID) == "" || strings.TrimSpace(connector.Label) == "" || !validDirections[connector.Direction] || !validDataTypes[connector.DataType] || !validSides[connector.Side] {
				return fmt.Errorf("nodes[%d].connectors[%d]: invalid connector", nodeIndex, connectorIndex)
			}
			if _, duplicate := connectors[connector.ID]; duplicate {
				return fmt.Errorf("nodes[%d].connectors: duplicate id %q", nodeIndex, connector.ID)
			}
			connectors[connector.ID] = connector
		}
		for key, value := range node.Configuration {
			switch typed := value.(type) {
			case nil, bool, string:
			case float64:
				if math.IsNaN(typed) || math.IsInf(typed, 0) {
					return fmt.Errorf("nodes[%d].configuration.%s: number must be finite", nodeIndex, key)
				}
			default:
				return fmt.Errorf("nodes[%d].configuration.%s: value must be a JSON scalar", nodeIndex, key)
			}
		}
		nodes[node.ID] = connectors
	}

	connectionIDs := make(map[string]bool, len(flow.Connections))
	for index, connection := range flow.Connections {
		if strings.TrimSpace(connection.ID) == "" || connectionIDs[connection.ID] {
			return fmt.Errorf("connections[%d]: id must be non-empty and unique", index)
		}
		connectionIDs[connection.ID] = true
		start, startOK := nodes[connection.Start.NodeID][connection.Start.ConnectorID]
		end, endOK := nodes[connection.End.NodeID][connection.End.ConnectorID]
		if !startOK || !endOK {
			return fmt.Errorf("connections[%d]: endpoint does not exist", index)
		}
		if start.Direction != "output" || end.Direction != "input" {
			return fmt.Errorf("connections[%d]: connection must run from output to input", index)
		}
		if start.DataType != "any" && end.DataType != "any" && start.DataType != end.DataType {
			return fmt.Errorf("connections[%d]: connector data types are incompatible", index)
		}
	}
	return nil
}
