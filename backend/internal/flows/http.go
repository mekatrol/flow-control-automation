package flows

import (
	"encoding/json"
	"errors"
	"io"
	"log"
	"net/http"
	"strings"
)

const maxRequestBytes = 10 << 20

type API struct{ store *Store }

func NewHandler(store *Store) http.Handler {
	api := &API{store: store}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/health", api.health)
	mux.HandleFunc("GET /api/flows", api.list)
	mux.HandleFunc("POST /api/flows", api.create)
	mux.HandleFunc("GET /api/flows/{flowId}", api.get)
	mux.HandleFunc("PUT /api/flows/{flowId}", api.save)
	mux.HandleFunc("DELETE /api/flows/{flowId}", api.delete)
	return mux
}

func (api *API) health(response http.ResponseWriter, _ *http.Request) {
	writeJSON(response, http.StatusOK, map[string]string{"status": "ok"})
}

func (api *API) list(response http.ResponseWriter, _ *http.Request) {
	writeJSON(response, http.StatusOK, api.store.List())
}

func (api *API) create(response http.ResponseWriter, request *http.Request) {
	var input struct {
		Name string `json:"name"`
	}
	if err := decodeJSON(response, request, &input); err != nil {
		writeError(response, http.StatusBadRequest, err.Error())
		return
	}
	if strings.TrimSpace(input.Name) == "" {
		writeError(response, http.StatusBadRequest, "name must be non-empty")
		return
	}
	flow, err := api.store.Create(input.Name)
	api.writeResult(response, flow, err, http.StatusCreated)
}

func (api *API) get(response http.ResponseWriter, request *http.Request) {
	flow, err := api.store.Get(request.PathValue("flowId"))
	api.writeResult(response, flow, err, http.StatusOK)
}

func (api *API) save(response http.ResponseWriter, request *http.Request) {
	var flow Flow
	if err := decodeJSON(response, request, &flow); err != nil {
		writeError(response, http.StatusBadRequest, err.Error())
		return
	}
	saved, err := api.store.Save(request.PathValue("flowId"), flow)
	api.writeResult(response, saved, err, http.StatusOK)
}

func (api *API) delete(response http.ResponseWriter, request *http.Request) {
	if err := api.store.Delete(request.PathValue("flowId")); err != nil {
		api.writeResult(response, Flow{}, err, http.StatusNoContent)
		return
	}
	response.WriteHeader(http.StatusNoContent)
}

func (api *API) writeResult(response http.ResponseWriter, flow Flow, err error, status int) {
	if errors.Is(err, ErrNotFound) {
		writeError(response, http.StatusNotFound, "flow not found")
		return
	}
	if err != nil {
		// Validation errors describe caller-controlled data. Persistence errors are
		// logged in full while the response avoids exposing host filesystem paths.
		if strings.Contains(err.Error(), "flow store") || strings.Contains(err.Error(), "directory") {
			log.Printf("persist flow: %v", err)
			writeError(response, http.StatusInternalServerError, "unable to persist flow")
			return
		}
		writeError(response, http.StatusBadRequest, err.Error())
		return
	}
	if status != http.StatusNoContent {
		writeJSON(response, status, flow)
	}
}

func decodeJSON(response http.ResponseWriter, request *http.Request, target any) error {
	request.Body = http.MaxBytesReader(response, request.Body, maxRequestBytes)
	decoder := json.NewDecoder(request.Body)
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(target); err != nil {
		return err
	}
	if err := decoder.Decode(&struct{}{}); !errors.Is(err, io.EOF) {
		return errors.New("request body must contain one JSON value")
	}
	return nil
}

func writeJSON(response http.ResponseWriter, status int, value any) {
	response.Header().Set("Content-Type", "application/json")
	response.WriteHeader(status)
	if err := json.NewEncoder(response).Encode(value); err != nil {
		log.Printf("write JSON response: %v", err)
	}
}

func writeError(response http.ResponseWriter, status int, message string) {
	writeJSON(response, status, map[string]string{"message": message})
}
