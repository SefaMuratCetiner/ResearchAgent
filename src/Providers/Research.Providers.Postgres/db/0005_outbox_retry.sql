-- Outbox retries: a message whose publish failed waits until next_attempt_at before it is tried again, so it never
-- holds back the messages behind it, and is parked (parked_at) once it used up its attempts instead of being retried
-- forever. Applied by tools/Research.DbScripts to every service database, after 0001_outbox_inbox.sql. Idempotent.

ALTER TABLE outbox_messages ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz NULL;
ALTER TABLE outbox_messages ADD COLUMN IF NOT EXISTS parked_at       timestamptz NULL;
