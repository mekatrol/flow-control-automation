package points

import (
	"context"
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"encoding/base64"
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

type CredentialMetadata struct {
	ID        string `json:"id"`
	Name      string `json:"name"`
	Kind      string `json:"kind"`
	Username  string `json:"username,omitempty"`
	Revision  int    `json:"revision"`
	CreatedAt string `json:"createdAt"`
	UpdatedAt string `json:"updatedAt"`
}

type CredentialInput struct {
	ID       string `json:"id"`
	Name     string `json:"name"`
	Kind     string `json:"kind"`
	Username string `json:"username,omitempty"`
	Password string `json:"password,omitempty"`
	Token    string `json:"token,omitempty"`
	Revision int    `json:"revision,omitempty"`
}

type encryptedCredential struct {
	CredentialMetadata
	Secret string `json:"secret"`
}

type credentialDocument struct {
	SchemaVersion int                   `json:"schemaVersion"`
	Credentials   []encryptedCredential `json:"credentials"`
}

type CredentialStore struct {
	mu          sync.RWMutex
	path        string
	key         []byte
	credentials map[string]encryptedCredential
	sources     *Store
	now         func() time.Time
}

func OpenCredentialStore(path, keyPath string, sources *Store) (*CredentialStore, error) {
	key, err := loadOrCreateCredentialKey(keyPath)
	if err != nil {
		return nil, err
	}
	store := &CredentialStore{
		path: path, key: key, credentials: map[string]encryptedCredential{},
		sources: sources, now: time.Now,
	}
	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return store, nil
	}
	if err != nil {
		return nil, fmt.Errorf("read credential store: %w", err)
	}
	var document credentialDocument
	if err := json.Unmarshal(data, &document); err != nil {
		return nil, fmt.Errorf("decode credential store: %w", err)
	}
	if document.SchemaVersion != 1 {
		return nil, errors.New("unsupported credential schemaVersion")
	}
	for _, credential := range document.Credentials {
		if _, duplicate := store.credentials[credential.ID]; duplicate {
			return nil, errors.New("duplicate stored credential ID")
		}
		if _, err := store.decrypt(credential.Secret); err != nil {
			return nil, fmt.Errorf("decrypt stored credential %q: %w", credential.ID, err)
		}
		store.credentials[credential.ID] = credential
	}
	return store, nil
}

func loadOrCreateCredentialKey(path string) ([]byte, error) {
	key, err := os.ReadFile(path)
	if err == nil {
		if len(key) != 32 {
			return nil, errors.New("credential encryption key must be 32 bytes")
		}
		return key, nil
	}
	if !errors.Is(err, os.ErrNotExist) {
		return nil, fmt.Errorf("read credential encryption key: %w", err)
	}
	if err := os.MkdirAll(filepath.Dir(path), 0o700); err != nil {
		return nil, fmt.Errorf("create credential key directory: %w", err)
	}
	key = make([]byte, 32)
	if _, err := rand.Read(key); err != nil {
		return nil, fmt.Errorf("generate credential encryption key: %w", err)
	}
	file, err := os.OpenFile(path, os.O_WRONLY|os.O_CREATE|os.O_EXCL, 0o600)
	if err != nil {
		return nil, fmt.Errorf("create credential encryption key: %w", err)
	}
	if _, err := file.Write(key); err != nil {
		file.Close()
		return nil, fmt.Errorf("write credential encryption key: %w", err)
	}
	if err := file.Close(); err != nil {
		return nil, fmt.Errorf("close credential encryption key: %w", err)
	}
	return key, nil
}

func (store *CredentialStore) List() []CredentialMetadata {
	store.mu.RLock()
	defer store.mu.RUnlock()
	items := make([]CredentialMetadata, 0, len(store.credentials))
	for _, credential := range store.credentials {
		items = append(items, credential.CredentialMetadata)
	}
	sort.Slice(items, func(i, j int) bool { return strings.ToLower(items[i].Name) < strings.ToLower(items[j].Name) })
	return items
}

func (store *CredentialStore) Get(id string) (CredentialMetadata, error) {
	store.mu.RLock()
	defer store.mu.RUnlock()
	credential, ok := store.credentials[id]
	if !ok {
		return CredentialMetadata{}, ErrNotFound
	}
	return credential.CredentialMetadata, nil
}

func (store *CredentialStore) Create(input CredentialInput) (CredentialMetadata, error) {
	store.mu.Lock()
	defer store.mu.Unlock()
	if err := validateCredentialInput(input, false); err != nil {
		return CredentialMetadata{}, err
	}
	if _, exists := store.credentials[input.ID]; exists || store.nameExists(input.Name, "") {
		return CredentialMetadata{}, fmt.Errorf("%w: credential ID or name already exists", ErrConflict)
	}
	secret, err := store.encrypt(secretValue(input))
	if err != nil {
		return CredentialMetadata{}, err
	}
	now := store.now().UTC().Format(time.RFC3339Nano)
	credential := encryptedCredential{
		CredentialMetadata: CredentialMetadata{
			ID: input.ID, Name: strings.TrimSpace(input.Name), Kind: input.Kind,
			Username: input.Username, Revision: 1, CreatedAt: now, UpdatedAt: now,
		},
		Secret: secret,
	}
	store.credentials[input.ID] = credential
	if err := store.persistLocked(); err != nil {
		delete(store.credentials, input.ID)
		return CredentialMetadata{}, err
	}
	return credential.CredentialMetadata, nil
}

func (store *CredentialStore) Update(id string, input CredentialInput) (CredentialMetadata, error) {
	store.mu.Lock()
	defer store.mu.Unlock()
	previous, exists := store.credentials[id]
	if !exists {
		return CredentialMetadata{}, ErrNotFound
	}
	if input.ID != id || input.Revision != previous.Revision {
		return CredentialMetadata{}, fmt.Errorf("%w: stale revision or mismatched ID", ErrConflict)
	}
	if err := validateCredentialInput(input, true); err != nil {
		return CredentialMetadata{}, err
	}
	if store.nameExists(input.Name, id) {
		return CredentialMetadata{}, fmt.Errorf("%w: credential name already exists", ErrConflict)
	}
	secret := previous.Secret
	if input.Password != "" || input.Token != "" {
		var err error
		secret, err = store.encrypt(secretValue(input))
		if err != nil {
			return CredentialMetadata{}, err
		}
	}
	updated := encryptedCredential{
		CredentialMetadata: CredentialMetadata{
			ID: id, Name: strings.TrimSpace(input.Name), Kind: input.Kind,
			Username: input.Username, Revision: previous.Revision + 1,
			CreatedAt: previous.CreatedAt, UpdatedAt: store.now().UTC().Format(time.RFC3339Nano),
		},
		Secret: secret,
	}
	store.credentials[id] = updated
	if err := store.persistLocked(); err != nil {
		store.credentials[id] = previous
		return CredentialMetadata{}, err
	}
	return updated.CredentialMetadata, nil
}

func (store *CredentialStore) Delete(id string, revision int) error {
	store.mu.Lock()
	defer store.mu.Unlock()
	previous, exists := store.credentials[id]
	if !exists {
		return ErrNotFound
	}
	if revision != previous.Revision {
		return fmt.Errorf("%w: stale revision", ErrConflict)
	}
	reference := "secret://" + id
	for _, source := range store.sources.AllSources() {
		if source.CredentialRef == reference {
			return fmt.Errorf("%w: credential is referenced by point source %q", ErrConflict, source.ID)
		}
	}
	delete(store.credentials, id)
	if err := store.persistLocked(); err != nil {
		store.credentials[id] = previous
		return err
	}
	return nil
}

func (store *CredentialStore) Resolve(_ context.Context, reference string) (string, error) {
	id := strings.TrimPrefix(reference, "secret://")
	if id == reference {
		return "", errors.New("unsupported credential reference")
	}
	store.mu.RLock()
	credential, exists := store.credentials[id]
	store.mu.RUnlock()
	if !exists {
		return "", errors.New("referenced credential is unavailable")
	}
	secret, err := store.decrypt(credential.Secret)
	if err != nil {
		return "", errors.New("referenced credential could not be resolved")
	}
	if credential.Kind == "mqtt" {
		data, _ := json.Marshal(map[string]string{"username": credential.Username, "password": secret})
		return string(data), nil
	}
	return secret, nil
}

func validateCredentialInput(input CredentialInput, update bool) error {
	if !validID.MatchString(input.ID) || strings.TrimSpace(input.Name) == "" {
		return errors.New("id and name are required; id must be lowercase and hyphenated")
	}
	if input.Kind != "mqtt" && input.Kind != "token" {
		return errors.New("kind must be mqtt or token")
	}
	if input.Kind == "mqtt" && strings.TrimSpace(input.Username) == "" {
		return errors.New("username is required for MQTT credentials")
	}
	if !update && secretValue(input) == "" {
		return errors.New("a password or token is required")
	}
	if input.Kind == "mqtt" && input.Token != "" || input.Kind == "token" && input.Password != "" {
		return errors.New("credential contains fields for a different kind")
	}
	return nil
}

func secretValue(input CredentialInput) string {
	if input.Kind == "mqtt" {
		return input.Password
	}
	return input.Token
}

func (store *CredentialStore) encrypt(value string) (string, error) {
	block, err := aes.NewCipher(store.key)
	if err != nil {
		return "", err
	}
	aead, err := cipher.NewGCM(block)
	if err != nil {
		return "", err
	}
	nonce := make([]byte, aead.NonceSize())
	if _, err := rand.Read(nonce); err != nil {
		return "", err
	}
	sealed := aead.Seal(nonce, nonce, []byte(value), nil)
	return base64.RawStdEncoding.EncodeToString(sealed), nil
}

func (store *CredentialStore) decrypt(encoded string) (string, error) {
	data, err := base64.RawStdEncoding.DecodeString(encoded)
	if err != nil {
		return "", err
	}
	block, err := aes.NewCipher(store.key)
	if err != nil {
		return "", err
	}
	aead, err := cipher.NewGCM(block)
	if err != nil || len(data) < aead.NonceSize() {
		return "", errors.New("invalid encrypted credential")
	}
	value, err := aead.Open(nil, data[:aead.NonceSize()], data[aead.NonceSize():], nil)
	return string(value), err
}

func (store *CredentialStore) nameExists(name, except string) bool {
	for id, credential := range store.credentials {
		if id != except && strings.EqualFold(credential.Name, name) {
			return true
		}
	}
	return false
}

func (store *CredentialStore) persistLocked() error {
	if err := os.MkdirAll(filepath.Dir(store.path), 0o700); err != nil {
		return fmt.Errorf("create credential directory: %w", err)
	}
	items := make([]encryptedCredential, 0, len(store.credentials))
	for _, credential := range store.credentials {
		items = append(items, credential)
	}
	sort.Slice(items, func(i, j int) bool { return items[i].ID < items[j].ID })
	data, err := json.MarshalIndent(credentialDocument{SchemaVersion: 1, Credentials: items}, "", "  ")
	if err != nil {
		return err
	}
	temporary, err := os.CreateTemp(filepath.Dir(store.path), ".credentials-*.json")
	if err != nil {
		return fmt.Errorf("create temporary credential store: %w", err)
	}
	_ = temporary.Chmod(0o600)
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
		return fmt.Errorf("persist credential store: %w", err)
	}
	return nil
}

type CombinedCredentials struct {
	Environment EnvironmentCredentials
	Vault       *CredentialStore
}

func (resolver CombinedCredentials) Resolve(ctx context.Context, reference string) (string, error) {
	if strings.HasPrefix(reference, "secret://") {
		return resolver.Vault.Resolve(ctx, reference)
	}
	return resolver.Environment.Resolve(ctx, reference)
}
