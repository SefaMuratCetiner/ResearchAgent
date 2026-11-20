-- research_fetching: pages fetched per job. The page text is in blob storage (blob_key); rows are small.
-- Applied by tools/Research.DbScripts after 0001_outbox_inbox.sql. Idempotent.

CREATE TABLE IF NOT EXISTS fetched_documents (
    id              uuid        PRIMARY KEY,
    job_id          uuid        NOT NULL,
    url             text        NOT NULL,
    url_hash        char(64)    NOT NULL,
    final_url       text        NOT NULL,
    content_hash    char(64)    NOT NULL,
    title           text        NOT NULL,
    mime_type       text        NOT NULL,
    size_bytes      bigint      NOT NULL CHECK (size_bytes >= 0),
    character_count integer     NOT NULL CHECK (character_count >= 0),
    blob_key        text        NOT NULL,
    created_at      timestamptz NOT NULL,
    created_by      text        NOT NULL,
    updated_at      timestamptz NULL,
    updated_by      text        NULL,
    CONSTRAINT uq_fetched_documents_job_url UNIQUE (job_id, url_hash)
);

CREATE INDEX IF NOT EXISTS ix_fetched_documents_content_hash ON fetched_documents (content_hash);
