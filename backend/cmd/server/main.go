package main

import (
	"encoding/json"
	"log"
	"net/http"
	"os"
)

const defaultAddress = ":8080"

func main() {
	address := os.Getenv("SERVER_ADDRESS")
	if address == "" {
		address = defaultAddress
	}

	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/health", healthHandler)

	server := &http.Server{
		Addr:    address,
		Handler: mux,
	}

	log.Printf("Go server listening on http://localhost%s", address)
	if err := server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}

func healthHandler(response http.ResponseWriter, _ *http.Request) {
	response.Header().Set("Content-Type", "application/json")

	if err := json.NewEncoder(response).Encode(map[string]string{"status": "ok"}); err != nil {
		log.Printf("write health response: %v", err)
	}
}
