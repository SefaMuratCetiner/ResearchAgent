-- research_knowledge: documents indexed once per (content hash, embedding model), their passages with embeddings,
-- and which job uses which document. Requires the pgvector extension (the AppHost runs the pgvector/pgvector image).
-- Applied by tools/Research.DbScripts after 0001_outbox_inbox.sql. Idempotent.
-- The vector size (384) matches the mock embedding model; a model with another size needs a new script.

CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS documents (
    id              uuid        PRIMARY KEY,
    content_hash    char(64)    NOT NULL,
    embedding_model text        NOT NULL,
    url             text        NOT NULL,
    title           text        NOT NULL,
    character_count integer     NOT NULL CHECK (character_count >= 0),
    passage_count   integer     NOT NULL CHECK (passage_count >= 0),
    created_at      timestamptz NOT NULL,
    created_by      text        NOT NULL,
    updated_at      timestamptz NULL,
    updated_by      text        NULL,
    CONSTRAINT uq_documents_content_model UNIQUE (content_hash, embedding_model)
);

CREATE TABLE IF NOT EXISTS passages (
    id          uuid        PRIMARY KEY,
    document_id uuid        NOT NULL REFERENCES documents (id) ON DELETE CASCADE,
    ordinal     integer     NOT NULL CHECK (ordinal >= 0),
    text        text        NOT NULL,
    embedding   vector(384) NOT NULL,
    CONSTRAINT uq_passages_document_ordinal UNIQUE (document_id, ordinal)
);

-- Approximate nearest neighbour on cosine distance. Searches are filtered to one job's documents, so the planner may
-- prefer the exact scan through ix_job_documents for small jobs; both are correct.
CREATE INDEX IF NOT EXISTS ix_passages_embedding ON passages USING hnsw (embedding vector_cosine_ops);

CREATE TABLE IF NOT EXISTS job_documents (
    job_id      uuid        NOT NULL,
    document_id uuid        NOT NULL REFERENCES documents (id) ON DELETE CASCADE,
    url         text        NOT NULL,
    title       text        NOT NULL,
    created_at  timestamptz NOT NULL,
    created_by  text        NOT NULL,
    PRIMARY KEY (job_id, document_id)
);

CREATE INDEX IF NOT EXISTS ix_job_documents_document ON job_documents (document_id);
