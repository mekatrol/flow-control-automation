package main

import (
	"log"
	"net/http"
	"os"

	"github.com/mekatrol/flow-control-automation/backend/internal/flows"
	"github.com/mekatrol/flow-control-automation/backend/internal/points"
)

const defaultAddress = ":8080"
const defaultFlowDataFile = "data/flows.json"
const defaultPointSourceDataFile = "data/point-sources.json"
const defaultCredentialDataFile = "data/credentials.json"
const defaultCredentialKeyFile = "data/credential.key"

func main() {
	address := os.Getenv("SERVER_ADDRESS")
	if address == "" {
		address = defaultAddress
	}

	dataFile := os.Getenv("FLOW_DATA_FILE")
	if dataFile == "" {
		dataFile = defaultFlowDataFile
	}
	flowStore, err := flows.OpenStore(dataFile)
	if err != nil {
		log.Fatalf("open flow store: %v", err)
	}
	sourceDataFile := os.Getenv("POINT_SOURCE_DATA_FILE")
	if sourceDataFile == "" {
		sourceDataFile = defaultPointSourceDataFile
	}
	sourceStore, err := points.OpenSourceStore(sourceDataFile)
	if err != nil {
		log.Fatalf("open point source store: %v", err)
	}
	credentialDataFile := environmentOrDefault("CREDENTIAL_DATA_FILE", defaultCredentialDataFile)
	credentialKeyFile := environmentOrDefault("CREDENTIAL_KEY_FILE", defaultCredentialKeyFile)
	credentialStore, err := points.OpenCredentialStore(credentialDataFile, credentialKeyFile, sourceStore)
	if err != nil {
		log.Fatalf("open credential store: %v", err)
	}
	resolver := points.CombinedCredentials{Environment: points.EnvironmentCredentials{}, Vault: credentialStore}
	sourceHandler := points.NewSourceHandler(sourceStore, points.NewTester(resolver))
	credentialHandler := points.NewCredentialHandler(credentialStore)
	mux := http.NewServeMux()
	mux.Handle("/api/point-sources", sourceHandler)
	mux.Handle("/api/point-sources/", sourceHandler)
	mux.Handle("/api/credentials", credentialHandler)
	mux.Handle("/api/credentials/", credentialHandler)
	mux.Handle("/", flows.NewHandler(flowStore))

	server := &http.Server{
		Addr:    address,
		Handler: mux,
	}

	log.Printf("Go server listening on http://localhost%s", address)
	if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}

func environmentOrDefault(name, fallback string) string {
	if value := os.Getenv(name); value != "" {
		return value
	}
	return fallback
}
