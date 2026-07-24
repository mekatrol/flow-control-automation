package flows

import (
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"sync"
	"time"
	"unicode"
)

var ErrNotFound = errors.New("flow not found")

type ListOptions struct {
	Filter        string
	Statuses      []string
	Page          int
	PageSize      int
	SortDirection string
}

type FlowPage struct {
	Items      []Flow `json:"items"`
	TotalItems int    `json:"totalItems"`
	Page       int    `json:"page"`
	PageSize   int    `json:"pageSize"`
	PageCount  int    `json:"pageCount"`
}

type Store struct {
	mu    sync.RWMutex
	path  string
	flows map[string]Flow
	now   func() time.Time
}

func OpenStore(path string) (*Store, error) {
	store := &Store{path: path, flows: make(map[string]Flow), now: time.Now}
	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return store, nil
	}
	if err != nil {
		return nil, fmt.Errorf("read flow store: %w", err)
	}
	var saved []Flow
	if err := json.Unmarshal(data, &saved); err != nil {
		return nil, fmt.Errorf("decode flow store: %w", err)
	}
	for _, flow := range saved {
		if err := flow.Validate(); err != nil {
			return nil, fmt.Errorf("validate stored flow %q: %w", flow.ID, err)
		}
		if _, duplicate := store.flows[flow.ID]; duplicate {
			return nil, fmt.Errorf("duplicate stored flow id %q", flow.ID)
		}
		store.flows[flow.ID] = flow
	}
	return store, nil
}

func (store *Store) List() []Flow {
	store.mu.RLock()
	defer store.mu.RUnlock()
	result := make([]Flow, 0, len(store.flows))
	for _, flow := range store.flows {
		result = append(result, flow)
	}
	sort.Slice(result, func(i, j int) bool { return result[i].Name < result[j].Name })
	return result
}

func (store *Store) ListPage(options ListOptions) FlowPage {
	store.mu.RLock()
	defer store.mu.RUnlock()

	filter := strings.ToLower(strings.TrimSpace(options.Filter))
	statuses := make(map[string]bool, len(options.Statuses))
	for _, status := range options.Statuses {
		statuses[status] = true
	}
	matches := make([]Flow, 0, len(store.flows))
	for _, flow := range store.flows {
		nameMatches := filter == "" || strings.Contains(strings.ToLower(flow.Name), filter)
		statusMatches := len(statuses) == 0 || statuses[flow.Status]
		if nameMatches && statusMatches {
			matches = append(matches, flow)
		}
	}
	sort.Slice(matches, func(i, j int) bool {
		left := strings.ToLower(matches[i].Name)
		right := strings.ToLower(matches[j].Name)
		if left == right {
			left, right = matches[i].ID, matches[j].ID
		}
		if options.SortDirection == "descending" {
			return left > right
		}
		return left < right
	})

	pageCount := max(1, (len(matches)+options.PageSize-1)/options.PageSize)
	page := min(max(1, options.Page), pageCount)
	start := min((page-1)*options.PageSize, len(matches))
	end := min(start+options.PageSize, len(matches))
	return FlowPage{
		Items:      matches[start:end],
		TotalItems: len(matches),
		Page:       page,
		PageSize:   options.PageSize,
		PageCount:  pageCount,
	}
}

func (store *Store) Get(id string) (Flow, error) {
	store.mu.RLock()
	defer store.mu.RUnlock()
	flow, found := store.flows[id]
	if !found {
		return Flow{}, ErrNotFound
	}
	return flow, nil
}

func (store *Store) Create(name string) (Flow, error) {
	store.mu.Lock()
	defer store.mu.Unlock()
	base := slug(name)
	if base == "" {
		base = "flow"
	}
	id := base
	for suffix := 2; ; suffix++ {
		if _, exists := store.flows[id]; !exists {
			break
		}
		id = fmt.Sprintf("%s-%d", base, suffix)
	}
	flow := Flow{ID: id, Name: strings.TrimSpace(name), Description: "", Status: "draft", UpdatedAt: store.timestamp(), Nodes: []Node{}, Connections: []Connection{}}
	if err := flow.Validate(); err != nil {
		return Flow{}, err
	}
	store.flows[id] = flow
	if err := store.persistLocked(); err != nil {
		delete(store.flows, id)
		return Flow{}, err
	}
	return flow, nil
}

func (store *Store) Save(id string, flow Flow) (Flow, error) {
	store.mu.Lock()
	defer store.mu.Unlock()
	previous, found := store.flows[id]
	if !found {
		return Flow{}, ErrNotFound
	}
	if flow.ID != id {
		return Flow{}, errors.New("flow id must match the request path")
	}
	flow.UpdatedAt = store.timestamp()
	if err := flow.Validate(); err != nil {
		return Flow{}, err
	}
	store.flows[id] = flow
	if err := store.persistLocked(); err != nil {
		store.flows[id] = previous
		return Flow{}, err
	}
	return flow, nil
}

func (store *Store) Delete(id string) error {
	store.mu.Lock()
	defer store.mu.Unlock()
	previous, found := store.flows[id]
	if !found {
		return ErrNotFound
	}
	delete(store.flows, id)
	if err := store.persistLocked(); err != nil {
		store.flows[id] = previous
		return err
	}
	return nil
}

func (store *Store) timestamp() string { return store.now().UTC().Format(time.RFC3339Nano) }

func (store *Store) persistLocked() error {
	if err := os.MkdirAll(filepath.Dir(store.path), 0o755); err != nil {
		return fmt.Errorf("create flow data directory: %w", err)
	}
	data, err := json.MarshalIndent(store.ListUnsafe(), "", "  ")
	if err != nil {
		return fmt.Errorf("encode flow store: %w", err)
	}
	temporary, err := os.CreateTemp(filepath.Dir(store.path), ".flows-*.json")
	if err != nil {
		return fmt.Errorf("create temporary flow store: %w", err)
	}
	temporaryPath := temporary.Name()
	defer os.Remove(temporaryPath)
	if _, err := temporary.Write(append(data, '\n')); err != nil {
		temporary.Close()
		return fmt.Errorf("write flow store: %w", err)
	}
	if err := temporary.Sync(); err != nil {
		temporary.Close()
		return fmt.Errorf("sync flow store: %w", err)
	}
	if err := temporary.Close(); err != nil {
		return fmt.Errorf("close flow store: %w", err)
	}
	if err := os.Rename(temporaryPath, store.path); err != nil {
		return fmt.Errorf("replace flow store: %w", err)
	}
	return nil
}

func (store *Store) ListUnsafe() []Flow {
	result := make([]Flow, 0, len(store.flows))
	for _, flow := range store.flows {
		result = append(result, flow)
	}
	sort.Slice(result, func(i, j int) bool { return result[i].ID < result[j].ID })
	return result
}

func slug(name string) string {
	var result strings.Builder
	dash := false
	for _, character := range strings.ToLower(strings.TrimSpace(name)) {
		if unicode.IsLetter(character) || unicode.IsDigit(character) {
			if dash && result.Len() > 0 {
				result.WriteByte('-')
			}
			result.WriteRune(character)
			dash = false
		} else {
			dash = true
		}
	}
	return result.String()
}
