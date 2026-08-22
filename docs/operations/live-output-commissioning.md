# Live-output commissioning

Live-output debugging must not be enabled for a controller model until this
checklist passes on an isolated test rig with outputs disconnected from
hazardous equipment. Record firmware revision, controller serial number, test
date, operator, and results in the release evidence.

Use an artifact whose output points and expected states are independently
reviewed. The authenticated client requires every affected point to be named:

```bash
./scripts/fcp-client.py "$FCP_PORT" debug-live-step --address 0 \
  --file artifact.bin --key "$FCP_KEY" --confirm-output output-01
```

Verify all of the following with electrical measurement rather than UI state:

1. An incorrect, missing, reordered, or additional confirmation point rejects
   enablement and leaves every output unchanged.
2. A higher-priority owner wins arbitration; the snapshot arbitration-loss
   counter increments and the debug owner does not disturb the winner.
3. Manual Step returns the physical output to its prior arbitrated state before
   the command completes.
4. Continuous Run refreshes commands, while Pause relinquishes every affected
   point immediately.
5. Stop, session replacement, evaluator fault, input-quality fault, and output
   write failure each relinquish every affected point.
6. Removing RS485 connectivity causes lease expiry and relinquishment within
   30 seconds. No command survives its 1000 ms expiry.
7. Resetting or power-cycling the controller clears the volatile session and
   restores the normal arbitration baseline without modifying durable flow
   metadata.
8. Emergency removal of controller power and restoration produces the board's
   documented safe output state.

Any failure blocks live-output capability approval for that controller model.
Shadow debugging remains available independently.
