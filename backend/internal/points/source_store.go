package points

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
)

type sourceDocument struct {
	SchemaVersion int      `json:"schemaVersion"`
	Revision      int      `json:"revision"`
	Sources       []Source `json:"sources"`
}

type SourcePage struct {
	Items      []Source `json:"items"`
	TotalItems int      `json:"totalItems"`
	Page       int      `json:"page"`
	PageSize   int      `json:"pageSize"`
	PageCount  int      `json:"pageCount"`
}

type Store struct {
	mu       sync.RWMutex
	path     string
	revision int
	sources  map[string]Source
	now      func() time.Time
}

func OpenSourceStore(path string) (*Store, error) {
	store := &Store{path: path, revision: 1, sources: map[string]Source{}, now: time.Now}
	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return store, nil
	}
	if err != nil {
		return nil, fmt.Errorf("read point source store: %w", err)
	}
	var document sourceDocument
	decoder := json.NewDecoder(strings.NewReader(string(data)))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&document); err != nil {
		return nil, fmt.Errorf("decode point source store: %w", err)
	}
	if document.SchemaVersion != SchemaVersion {
		return nil, errors.New("unsupported point source schemaVersion")
	}
	store.revision = document.Revision
	names := map[string]bool{}
	for _, source := range document.Sources {
		if err := source.Validate(); err != nil {
			return nil, fmt.Errorf("validate stored source %q: %w", source.ID, err)
		}
		name := strings.ToLower(source.Name)
		if _, exists := store.sources[source.ID]; exists || names[name] {
			return nil, errors.New("duplicate stored source ID or name")
		}
		store.sources[source.ID], names[name] = source, true
	}
	return store, nil
}

func (store *Store) List(filter, direction string, page, pageSize int) SourcePage {
	store.mu.RLock()
	defer store.mu.RUnlock()
	items := make([]Source, 0, len(store.sources))
	filter = strings.ToLower(strings.TrimSpace(filter))
	for _, source := range store.sources {
		if filter == "" || strings.Contains(strings.ToLower(source.Name), filter) {
			items = append(items, source)
		}
	}
	sort.Slice(items, func(i, j int) bool {
		less := strings.ToLower(items[i].Name) < strings.ToLower(items[j].Name)
		if direction == "descending" {
			return !less
		}
		return less
	})
	count := max(1, (len(items)+pageSize-1)/pageSize)
	page = min(max(1, page), count)
	start, end := min((page-1)*pageSize, len(items)), min(page*pageSize, len(items))
	return SourcePage{Items: items[start:end], TotalItems: len(items), Page: page, PageSize: pageSize, PageCount: count}
}

func (store *Store) Get(id string) (Source, error) {
	store.mu.RLock()
	defer store.mu.RUnlock()
	source, ok := store.sources[id]
	if !ok {
		return Source{}, ErrNotFound
	}
	return source, nil
}

func (store *Store) AllSources() []Source {
	store.mu.RLock()
	defer store.mu.RUnlock()
	items := make([]Source, 0, len(store.sources))
	for _, source := range store.sources {
		items = append(items, source)
	}
	return items
}

func (store *Store) Create(source Source) (Source, error) {
	store.mu.Lock()
	defer store.mu.Unlock()
	if err := source.Validate(); err != nil {
		return Source{}, err
	}
	if _, ok := store.sources[source.ID]; ok || store.nameExists(source.Name, "") {
		return Source{}, fmt.Errorf("%w: source ID or name already exists", ErrConflict)
	}
	now := store.now().UTC().Format(time.RFC3339Nano)
	source.Revision, source.CreatedAt, source.UpdatedAt = 1, now, now
	store.sources[source.ID] = source
	if err := store.persistLocked(); err != nil {
		delete(store.sources, source.ID)
		return Source{}, err
	}
	return source, nil
}

func (store *Store) Update(id string, source Source) (Source, error) {
	store.mu.Lock()
	defer store.mu.Unlock()
	previous, ok := store.sources[id]
	if !ok {
		return Source{}, ErrNotFound
	}
	if source.ID != id {
		return Source{}, errors.New("source id must match request path")
	}
	if source.Revision != previous.Revision {
		return Source{}, fmt.Errorf("%w: stale revision", ErrConflict)
	}
	if err := source.Validate(); err != nil {
		return Source{}, err
	}
	if store.nameExists(source.Name, id) {
		return Source{}, fmt.Errorf("%w: source name already exists", ErrConflict)
	}
	source.Revision++
	source.CreatedAt, source.UpdatedAt = previous.CreatedAt, store.now().UTC().Format(time.RFC3339Nano)
	store.sources[id] = source
	if err := store.persistLocked(); err != nil {
		store.sources[id] = previous
		return Source{}, err
	}
	return source, nil
}

func (store *Store) Delete(id string, revision int) error {
	store.mu.Lock()
	defer store.mu.Unlock()
	previous, ok := store.sources[id]
	if !ok {
		return ErrNotFound
	}
	if previous.Revision != revision {
		return fmt.Errorf("%w: stale revision", ErrConflict)
	}
	delete(store.sources, id)
	if err := store.persistLocked(); err != nil {
		store.sources[id] = previous
		return err
	}
	return nil
}

func (store *Store) nameExists(name, except string) bool {
	for id, source := range store.sources {
		if id != except && strings.EqualFold(source.Name, name) {
			return true
		}
	}
	return false
}

func (store *Store) persistLocked() error {
	if err := os.MkdirAll(filepath.Dir(store.path), 0o755); err != nil {
		return fmt.Errorf("create point source directory: %w", err)
	}
	items := make([]Source, 0, len(store.sources))
	for _, source := range store.sources {
		items = append(items, source)
	}
	sort.Slice(items, func(i, j int) bool { return items[i].ID < items[j].ID })
	data, err := json.MarshalIndent(sourceDocument{SchemaVersion: SchemaVersion, Revision: store.revision, Sources: items}, "", "  ")
	if err != nil {
		return fmt.Errorf("encode point source store: %w", err)
	}
	temporary, err := os.CreateTemp(filepath.Dir(store.path), ".point-sources-*.json")
	if err != nil {
		return fmt.Errorf("create temporary point source store: %w", err)
	}
	path := temporary.Name()
	defer os.Remove(path)
	if _, err = temporary.Write(append(data, '\n')); err == nil {
		err = temporary.Sync()
	}
	if closeErr := temporary.Close(); err == nil {
		err = closeErr
	}
	if err == nil {
		err = os.Rename(path, store.path)
	}
	if err != nil {
		return fmt.Errorf("persist point source store: %w", err)
	}
	return nil
}
