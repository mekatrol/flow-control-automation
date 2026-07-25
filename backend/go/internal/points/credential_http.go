package points

import (
	"encoding/json"
	"errors"
	"io"
	"log"
	"net/http"
	"strconv"
	"strings"
)

func NewCredentialHandler(store *CredentialStore) http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/credentials", func(response http.ResponseWriter, _ *http.Request) {
		writePointJSON(response, http.StatusOK, map[string]any{"items": store.List()})
	})
	mux.HandleFunc("POST /api/credentials", func(response http.ResponseWriter, request *http.Request) {
		input, err := decodeCredential(response, request)
		if err != nil {
			writePointError(response, http.StatusBadRequest, err.Error())
			return
		}
		metadata, err := store.Create(input)
		writeCredentialResult(response, metadata, err, http.StatusCreated)
	})
	mux.HandleFunc("GET /api/credentials/{credentialId}", func(response http.ResponseWriter, request *http.Request) {
		metadata, err := store.Get(request.PathValue("credentialId"))
		writeCredentialResult(response, metadata, err, http.StatusOK)
	})
	mux.HandleFunc("PUT /api/credentials/{credentialId}", func(response http.ResponseWriter, request *http.Request) {
		input, err := decodeCredential(response, request)
		if err != nil {
			writePointError(response, http.StatusBadRequest, err.Error())
			return
		}
		metadata, err := store.Update(request.PathValue("credentialId"), input)
		writeCredentialResult(response, metadata, err, http.StatusOK)
	})
	mux.HandleFunc("DELETE /api/credentials/{credentialId}", func(response http.ResponseWriter, request *http.Request) {
		revision, err := strconv.Atoi(request.URL.Query().Get("revision"))
		if err != nil {
			writePointError(response, http.StatusBadRequest, "revision must be an integer")
			return
		}
		if err := store.Delete(request.PathValue("credentialId"), revision); err != nil {
			writeCredentialResult(response, CredentialMetadata{}, err, http.StatusNoContent)
			return
		}
		response.WriteHeader(http.StatusNoContent)
	})
	return mux
}

func decodeCredential(response http.ResponseWriter, request *http.Request) (CredentialInput, error) {
	request.Body = http.MaxBytesReader(response, request.Body, 64<<10)
	decoder := json.NewDecoder(request.Body)
	decoder.DisallowUnknownFields()
	var input CredentialInput
	if err := decoder.Decode(&input); err != nil {
		return CredentialInput{}, err
	}
	if err := decoder.Decode(&struct{}{}); !errors.Is(err, io.EOF) {
		return CredentialInput{}, errors.New("request body must contain one JSON value")
	}
	return input, nil
}

func writeCredentialResult(response http.ResponseWriter, metadata CredentialMetadata, err error, status int) {
	if errors.Is(err, ErrNotFound) {
		writePointError(response, http.StatusNotFound, "credential not found")
		return
	}
	if errors.Is(err, ErrConflict) {
		writePointError(response, http.StatusConflict, strings.TrimPrefix(err.Error(), ErrConflict.Error()+": "))
		return
	}
	if err != nil {
		if strings.Contains(err.Error(), "store") || strings.Contains(err.Error(), "directory") {
			log.Printf("persist credential: %v", err)
			writePointError(response, http.StatusInternalServerError, "unable to persist credential")
		} else {
			writePointError(response, http.StatusBadRequest, err.Error())
		}
		return
	}
	writePointJSON(response, status, metadata)
}
