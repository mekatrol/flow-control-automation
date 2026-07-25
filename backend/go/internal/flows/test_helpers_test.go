package flows

import (
	"net/http"
	"path/filepath"
	"testing"
)

func newTestHandler(t *testing.T) (*Store, http.Handler) {
	t.Helper()
	store, err := OpenStore(filepath.Join(t.TempDir(), "flows.json"))
	if err != nil {
		t.Fatal(err)
	}
	return store, NewHandler(store)
}
