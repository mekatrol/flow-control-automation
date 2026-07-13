package flows

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"testing"
)

func TestFlowCRUDPersistsAcrossStoreRestart(t *testing.T) {
	dataFile := filepath.Join(t.TempDir(), "nested", "flows.json")
	store, err := OpenStore(dataFile)
	if err != nil {
		t.Fatal(err)
	}
	handler := NewHandler(store)

	created := requestFlow(t, handler, http.MethodPost, "/api/flows", `{"name":"Climate Control"}`, http.StatusCreated)
	if created.ID != "climate-control" || created.Status != "draft" || created.Nodes == nil {
		t.Fatalf("unexpected created flow: %#v", created)
	}

	created.Name = "Renamed climate"
	created.Description = "Persisted graph"
	created.Nodes = []Node{{
		ID: "pulse-1", Kind: "pulse", Label: "Every minute", X: 10, Y: 20, ZOrder: 1,
		Color: "#ffffff", Connectors: []Connector{}, Configuration: map[string]any{"interval": float64(60)},
	}}
	body, err := json.Marshal(created)
	if err != nil {
		t.Fatal(err)
	}
	saved := requestFlow(t, handler, http.MethodPut, "/api/flows/climate-control", string(body), http.StatusOK)
	if saved.Name != "Renamed climate" || saved.UpdatedAt == "" {
		t.Fatalf("unexpected saved flow: %#v", saved)
	}

	reopened, err := OpenStore(dataFile)
	if err != nil {
		t.Fatal(err)
	}
	loaded := requestFlow(t, NewHandler(reopened), http.MethodGet, "/api/flows/climate-control", "", http.StatusOK)
	if loaded.Description != "Persisted graph" || len(loaded.Nodes) != 1 {
		t.Fatalf("flow did not survive restart: %#v", loaded)
	}

	response := request(t, NewHandler(reopened), http.MethodDelete, "/api/flows/climate-control", "")
	if response.Code != http.StatusNoContent || response.Body.Len() != 0 {
		t.Fatalf("delete returned %d with %q", response.Code, response.Body.String())
	}
	requestStatus(t, NewHandler(reopened), http.MethodGet, "/api/flows/climate-control", "", http.StatusNotFound)
}

func TestCreateMakesUniqueReadableIDs(t *testing.T) {
	store, err := OpenStore(filepath.Join(t.TempDir(), "flows.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := NewHandler(store)
	first := requestFlow(t, handler, http.MethodPost, "/api/flows", `{"name":"Heating & Cooling"}`, http.StatusCreated)
	second := requestFlow(t, handler, http.MethodPost, "/api/flows", `{"name":"Heating & Cooling"}`, http.StatusCreated)
	if first.ID != "heating-cooling" || second.ID != "heating-cooling-2" {
		t.Fatalf("unexpected ids %q and %q", first.ID, second.ID)
	}
}

func TestSaveRejectsInvalidGraphWithoutReplacingStoredFlow(t *testing.T) {
	store, err := OpenStore(filepath.Join(t.TempDir(), "flows.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := NewHandler(store)
	created := requestFlow(t, handler, http.MethodPost, "/api/flows", `{"name":"Safe flow"}`, http.StatusCreated)
	created.Nodes = []Node{{
		ID: "bad", Kind: "unknown", Label: "Bad", Color: "#fff",
		Connectors: []Connector{}, Configuration: map[string]any{},
	}}
	body, err := json.Marshal(created)
	if err != nil {
		t.Fatal(err)
	}
	requestStatus(t, handler, http.MethodPut, "/api/flows/safe-flow", string(body), http.StatusBadRequest)
	loaded := requestFlow(t, handler, http.MethodGet, "/api/flows/safe-flow", "", http.StatusOK)
	if len(loaded.Nodes) != 0 {
		t.Fatalf("invalid update replaced stored flow: %#v", loaded)
	}
}

func TestSaveAcceptsAverageNodeEmittedByEditor(t *testing.T) {
	store, err := OpenStore(filepath.Join(t.TempDir(), "flows.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := NewHandler(store)
	created := requestFlow(t, handler, http.MethodPost, "/api/flows", `{"name":"Clothes line light"}`, http.StatusCreated)
	created.Nodes = []Node{{
		ID: "average-1", Kind: "average", Label: "Average", X: 10, Y: 20, ZOrder: 1,
		Color: "#ef8354", Connectors: []Connector{}, Configuration: map[string]any{"enabled": true},
	}}
	body, err := json.Marshal(created)
	if err != nil {
		t.Fatal(err)
	}
	saved := requestFlow(t, handler, http.MethodPut, "/api/flows/"+created.ID, string(body), http.StatusOK)
	if len(saved.Nodes) != 1 || saved.Nodes[0].Kind != "average" {
		t.Fatalf("average node was not saved: %#v", saved)
	}
}

func TestSaveRequiresMatchingPathIDAndExistingFlow(t *testing.T) {
	store, err := OpenStore(filepath.Join(t.TempDir(), "flows.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := NewHandler(store)
	created := requestFlow(t, handler, http.MethodPost, "/api/flows", `{"name":"One"}`, http.StatusCreated)
	body, _ := json.Marshal(created)
	requestStatus(t, handler, http.MethodPut, "/api/flows/missing", string(body), http.StatusNotFound)
	created.ID = "different"
	body, _ = json.Marshal(created)
	requestStatus(t, handler, http.MethodPut, "/api/flows/one", string(body), http.StatusBadRequest)
}

func TestRuntimeStartsStoppedAndDeploysSavedFlow(t *testing.T) {
	store, err := OpenStore(filepath.Join(t.TempDir(), "flows.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := NewHandler(store)
	created := requestFlow(t, handler, http.MethodPost, "/api/flows", `{"name":"Runtime flow"}`, http.StatusCreated)

	stopped := requestRuntime(t, handler, http.MethodGet, "/api/flows/"+created.ID+"/runtime", http.StatusOK)
	if stopped.FlowID != created.ID || stopped.State != "stopped" || stopped.Nodes == nil {
		t.Fatalf("unexpected initial runtime snapshot: %#v", stopped)
	}

	running := requestRuntime(t, handler, http.MethodPost, "/api/flows/"+created.ID+"/deploy", http.StatusOK)
	if running.FlowID != created.ID || running.State != "running" {
		t.Fatalf("unexpected deployed runtime snapshot: %#v", running)
	}
	reloaded := requestRuntime(t, handler, http.MethodGet, "/api/flows/"+created.ID+"/runtime", http.StatusOK)
	if reloaded.State != "running" {
		t.Fatalf("runtime state was not retained: %#v", reloaded)
	}
}

func TestRuntimeRoutesReturnNotFoundForMissingFlow(t *testing.T) {
	store, err := OpenStore(filepath.Join(t.TempDir(), "flows.json"))
	if err != nil {
		t.Fatal(err)
	}
	handler := NewHandler(store)
	requestStatus(t, handler, http.MethodGet, "/api/flows/missing/runtime", "", http.StatusNotFound)
	requestStatus(t, handler, http.MethodPost, "/api/flows/missing/deploy", "", http.StatusNotFound)
}

func requestRuntime(t *testing.T, handler http.Handler, method, path string, status int) RuntimeSnapshot {
	t.Helper()
	response := request(t, handler, method, path, "")
	if response.Code != status {
		t.Fatalf("%s %s returned %d: %s", method, path, response.Code, response.Body.String())
	}
	var snapshot RuntimeSnapshot
	if err := json.NewDecoder(response.Body).Decode(&snapshot); err != nil {
		t.Fatal(err)
	}
	return snapshot
}

func requestFlow(t *testing.T, handler http.Handler, method, path, body string, status int) Flow {
	t.Helper()
	response := request(t, handler, method, path, body)
	if response.Code != status {
		t.Fatalf("%s %s returned %d: %s", method, path, response.Code, response.Body.String())
	}
	var flow Flow
	if err := json.NewDecoder(response.Body).Decode(&flow); err != nil {
		t.Fatal(err)
	}
	return flow
}

func requestStatus(t *testing.T, handler http.Handler, method, path, body string, status int) {
	t.Helper()
	response := request(t, handler, method, path, body)
	if response.Code != status {
		t.Fatalf("%s %s returned %d: %s", method, path, response.Code, response.Body.String())
	}
}

func request(t *testing.T, handler http.Handler, method, path, body string) *httptest.ResponseRecorder {
	t.Helper()
	request := httptest.NewRequest(method, path, bytes.NewBufferString(body))
	if body != "" {
		request.Header.Set("Content-Type", "application/json")
	}
	response := httptest.NewRecorder()
	handler.ServeHTTP(response, request)
	return response
}
