CREATE TABLE IF NOT EXISTS power_operations (
  id TEXT PRIMARY KEY,
  action TEXT NOT NULL,
  requested_by TEXT NOT NULL DEFAULT 'api',
  request_id TEXT,
  requested_at TEXT NOT NULL,
  scheduled_for TEXT NOT NULL,
  delay_seconds INTEGER NOT NULL DEFAULT 0,
  state TEXT NOT NULL DEFAULT 'accepted',
  result_message TEXT,
  reason TEXT,
  cancellable INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS audit_events (
  id TEXT PRIMARY KEY,
  request_id TEXT,
  actor_device_id TEXT NOT NULL DEFAULT 'api-token',
  action TEXT NOT NULL,
  target TEXT,
  result TEXT NOT NULL,
  redacted_details_json TEXT,
  created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_power_operations_state ON power_operations(state);
CREATE INDEX IF NOT EXISTS idx_audit_events_created_at ON audit_events(created_at);
