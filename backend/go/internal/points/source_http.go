package points

import (
	"encoding/json"
	"errors"
	"io"
	"log"
	"net"
	"net/http"
	"strconv"
	"strings"
)

type API struct {
	store  *Store
	tester *Tester
}

func NewSourceHandler(store *Store, tester *Tester) http.Handler {
	api := &API{store: store, tester: tester}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/point-sources", api.list)
	mux.HandleFunc("POST /api/point-sources", api.create)
	mux.HandleFunc("GET /api/point-sources/{sourceId}", api.get)
	mux.HandleFunc("PUT /api/point-sources/{sourceId}", api.update)
	mux.HandleFunc("DELETE /api/point-sources/{sourceId}", api.delete)
	mux.HandleFunc("POST /api/point-sources/test", api.testUnsaved)
	mux.HandleFunc("POST /api/point-sources/{sourceId}/test", api.testSaved)
	return mux
}

func (api *API) list(response http.ResponseWriter, request *http.Request) {
	page, pageSize := parsePositive(request.URL.Query().Get("page"), 1), parsePositive(request.URL.Query().Get("pageSize"), 10)
	sort := request.URL.Query().Get("sort")
	if sort == "" {
		sort = "ascending"
	}
	if page < 1 || !map[int]bool{10: true, 20: true, 50: true}[pageSize] || (sort != "ascending" && sort != "descending") {
		writePointError(response, http.StatusBadRequest, "invalid pagination or sort query")
		return
	}
	writePointJSON(response, http.StatusOK, api.store.List(request.URL.Query().Get("filter"), sort, page, pageSize))
}

func (api *API) create(response http.ResponseWriter, request *http.Request) {
	source, err := decodeSourceYAML(response, request)
	if err != nil {
		writePointError(response, http.StatusBadRequest, err.Error())
		return
	}
	saved, err := api.store.Create(source)
	api.writeSource(response, saved, err, http.StatusCreated)
}

func (api *API) get(response http.ResponseWriter, request *http.Request) {
	source, err := api.store.Get(request.PathValue("sourceId"))
	api.writeSource(response, source, err, http.StatusOK)
}

func (api *API) update(response http.ResponseWriter, request *http.Request) {
	source, err := decodeSourceYAML(response, request)
	if err != nil {
		writePointError(response, http.StatusBadRequest, err.Error())
		return
	}
	revision, err := strconv.Atoi(request.Header.Get("If-Match"))
	if err != nil {
		writePointError(response, http.StatusBadRequest, "If-Match must contain the last observed revision")
		return
	}
	source.Revision = revision
	saved, err := api.store.Update(request.PathValue("sourceId"), source)
	api.writeSource(response, saved, err, http.StatusOK)
}

func (api *API) delete(response http.ResponseWriter, request *http.Request) {
	revision, err := strconv.Atoi(request.URL.Query().Get("revision"))
	if err != nil {
		writePointError(response, http.StatusBadRequest, "revision must be an integer")
		return
	}
	if err := api.store.Delete(request.PathValue("sourceId"), revision); err != nil {
		api.writeSource(response, Source{}, err, http.StatusNoContent)
		return
	}
	response.WriteHeader(http.StatusNoContent)
}

func (api *API) testUnsaved(response http.ResponseWriter, request *http.Request) {
	source, err := decodeSourceYAML(response, request)
	if err != nil {
		writePointError(response, http.StatusBadRequest, err.Error())
		return
	}
	api.runTest(response, request, source)
}

func (api *API) testSaved(response http.ResponseWriter, request *http.Request) {
	source, err := api.store.Get(request.PathValue("sourceId"))
	if err != nil {
		api.writeSource(response, Source{}, err, http.StatusOK)
		return
	}
	api.runTest(response, request, source)
}

func (api *API) runTest(response http.ResponseWriter, request *http.Request, source Source) {
	key, _, _ := net.SplitHostPort(request.RemoteAddr)
	result := api.tester.Test(request.Context(), source, key)
	// Connectivity results are deliberately transient; only this redacted summary
	// is returned, and neither it nor resolved credentials enter the source store.
	log.Printf("point source connectivity test source=%q status=%s", source.ID, result.Status)
	writePointJSON(response, http.StatusOK, result)
}

func decodeSourceYAML(response http.ResponseWriter, request *http.Request) (Source, error) {
	request.Body = http.MaxBytesReader(response, request.Body, MaxYAMLBytes)
	data, err := io.ReadAll(request.Body)
	if err != nil {
		return Source{}, errors.New("unable to read YAML request")
	}
	return ParseSourceYAML(data)
}

func (api *API) writeSource(response http.ResponseWriter, source Source, err error, status int) {
	if errors.Is(err, ErrNotFound) {
		writePointError(response, http.StatusNotFound, "point source not found")
		return
	}
	if errors.Is(err, ErrConflict) {
		writePointError(response, http.StatusConflict, strings.TrimPrefix(err.Error(), ErrConflict.Error()+": "))
		return
	}
	if err != nil {
		if strings.Contains(err.Error(), "store") || strings.Contains(err.Error(), "directory") {
			log.Printf("persist point source: %v", err)
			writePointError(response, http.StatusInternalServerError, "unable to persist point source")
		} else {
			writePointError(response, http.StatusBadRequest, err.Error())
		}
		return
	}
	response.Header().Set("ETag", strconv.Itoa(source.Revision))
	response.Header().Set("Content-Type", "application/yaml")
	response.WriteHeader(status)
	data, marshalErr := RenderSourceYAML(source)
	if marshalErr == nil {
		_, _ = response.Write(data)
	}
}

func parsePositive(value string, fallback int) int {
	if value == "" {
		return fallback
	}
	result, _ := strconv.Atoi(value)
	return result
}

func writePointJSON(response http.ResponseWriter, status int, value any) {
	response.Header().Set("Content-Type", "application/json")
	response.WriteHeader(status)
	_ = json.NewEncoder(response).Encode(value)
}

func writePointError(response http.ResponseWriter, status int, message string) {
	writePointJSON(response, status, map[string]string{"message": message})
}
