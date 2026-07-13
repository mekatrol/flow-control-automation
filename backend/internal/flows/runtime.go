package flows

import (
	"sync"
	"time"
)

// RuntimeSnapshot is deliberately separate from Flow. Execution state is
// process-local telemetry and must never be written into the saved graph.
type RuntimeSnapshot struct {
	FlowID    string                         `json:"flowId"`
	State     string                         `json:"state"`
	UpdatedAt string                         `json:"updatedAt"`
	Nodes     map[string]NodeRuntimeSnapshot `json:"nodes"`
}

type NodeRuntimeSnapshot struct {
	State     string `json:"state"`
	UpdatedAt string `json:"updatedAt"`
}

type RuntimeStore struct {
	mu        sync.RWMutex
	snapshots map[string]RuntimeSnapshot
	now       func() time.Time
}

func NewRuntimeStore() *RuntimeStore {
	return &RuntimeStore{snapshots: make(map[string]RuntimeSnapshot), now: time.Now}
}

func (store *RuntimeStore) Get(flow Flow) RuntimeSnapshot {
	store.mu.RLock()
	snapshot, found := store.snapshots[flow.ID]
	store.mu.RUnlock()
	if found {
		return snapshot
	}
	return store.snapshot(flow, "stopped", "stopped")
}

func (store *RuntimeStore) Deploy(flow Flow) RuntimeSnapshot {
	snapshot := store.snapshot(flow, "running", "running")
	store.mu.Lock()
	store.snapshots[flow.ID] = snapshot
	store.mu.Unlock()
	return snapshot
}

func (store *RuntimeStore) Delete(flowID string) {
	store.mu.Lock()
	delete(store.snapshots, flowID)
	store.mu.Unlock()
}

func (store *RuntimeStore) snapshot(flow Flow, flowState, nodeState string) RuntimeSnapshot {
	updatedAt := store.now().UTC().Format(time.RFC3339Nano)
	nodes := make(map[string]NodeRuntimeSnapshot, len(flow.Nodes))
	for _, node := range flow.Nodes {
		nodes[node.ID] = NodeRuntimeSnapshot{State: nodeState, UpdatedAt: updatedAt}
	}
	return RuntimeSnapshot{FlowID: flow.ID, State: flowState, UpdatedAt: updatedAt, Nodes: nodes}
}
