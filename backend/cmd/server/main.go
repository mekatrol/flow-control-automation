package main

import (
	"log"
	"net/http"
	"os"

	"github.com/mekatrol/flow-control-automation/backend/internal/flows"
)

const defaultAddress = ":8080"
const defaultFlowDataFile = "data/flows.json"

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

	server := &http.Server{
		Addr:    address,
		Handler: flows.NewHandler(flowStore),
	}

	log.Printf("Go server listening on http://localhost%s", address)
	if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}
