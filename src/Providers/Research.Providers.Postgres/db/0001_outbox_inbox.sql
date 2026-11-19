-- Outbox and inbox: shared by every service database (research_jobs, research_fetching, research_knowledge).
-- Applied by tools/Research.DbScripts before the services start. Idempotent.

CREATE TABLE IF NOT EXISTS outbox_messages (
    id              uuid        PRIMARY KEY,
    message_type    text        NOT NULL,
    payload         jsonb       NOT NULL,
    headers         jsonb       NOT NULL,
    occurred_at     timestamptz NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    published_at    timestamptz NULL,
    attempts        integer     NOT NULL DEFAULT 0,
    last_error      text        NULL
);

-- The dispatcher's hot query: unpublished rows, oldest first.
CREATE INDEX IF NOT EXISTS ix_outbox_messages_pending
    ON outbox_messages (created_at, id)
    WHERE published_at IS NULL;

CREATE TABLE IF NOT EXISTS inbox_messages (
    message_id      uuid        NOT NULL,
    consumer        text        NOT NULL,
    processed_at    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (message_id, consumer)
);
