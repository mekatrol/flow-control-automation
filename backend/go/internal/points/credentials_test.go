package points

import (
	"context"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestCredentialStoreEncryptsSecretsAndNeverReturnsThem(t *testing.T) {
	directory := t.TempDir()
	sourceStore, err := OpenSourceStore(filepath.Join(directory, "sources.json"))
	if err != nil {
		t.Fatal(err)
	}
	store, err := OpenCredentialStore(
		filepath.Join(directory, "credentials.json"),
		filepath.Join(directory, "credential.key"),
		sourceStore,
	)
	if err != nil {
		t.Fatal(err)
	}
	metadata, err := store.Create(CredentialInput{
		ID: "plant-mqtt", Name: "Plant MQTT", Kind: "mqtt",
		Username: "reader", Password: "highly-secret",
	})
	if err != nil {
		t.Fatal(err)
	}
	serialized, err := os.ReadFile(filepath.Join(directory, "credentials.json"))
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(string(serialized), "highly-secret") {
		t.Fatal("credential store contains plaintext secret")
	}
	if metadata.Username != "reader" || metadata.Revision != 1 {
		t.Fatalf("unexpected metadata: %#v", metadata)
	}
	resolved, err := store.Resolve(context.Background(), "secret://plant-mqtt")
	if err != nil || !strings.Contains(resolved, `"password":"highly-secret"`) {
		t.Fatalf("credential did not resolve internally: %v", err)
	}
	reopened, err := OpenCredentialStore(
		filepath.Join(directory, "credentials.json"),
		filepath.Join(directory, "credential.key"),
		sourceStore,
	)
	if err != nil {
		t.Fatal(err)
	}
	if _, err := reopened.Resolve(context.Background(), "secret://plant-mqtt"); err != nil {
		t.Fatal(err)
	}
}

func TestCredentialDeletionIsBlockedByPointSourceReference(t *testing.T) {
	directory := t.TempDir()
	sourceStore, _ := OpenSourceStore(filepath.Join(directory, "sources.json"))
	store, err := OpenCredentialStore(
		filepath.Join(directory, "credentials.json"),
		filepath.Join(directory, "credential.key"),
		sourceStore,
	)
	if err != nil {
		t.Fatal(err)
	}
	credential, err := store.Create(CredentialInput{
		ID: "weather-token", Name: "Weather token", Kind: "token", Token: "secret",
	})
	if err != nil {
		t.Fatal(err)
	}
	source := validHTTPSource()
	source.CredentialRef = "secret://weather-token"
	if _, err := sourceStore.Create(source); err != nil {
		t.Fatal(err)
	}
	if err := store.Delete(credential.ID, credential.Revision); err == nil ||
		!strings.Contains(err.Error(), source.ID) {
		t.Fatalf("expected reference conflict, got %v", err)
	}
}
