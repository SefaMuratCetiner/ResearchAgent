-- research_jobs database: jobs, their steps and reports. Applied after 0001_outbox_inbox.sql. Idempotent.

CREATE TABLE IF NOT EXISTS research_jobs (
    id                    uuid        PRIMARY KEY,
    question              text        NOT NULL,
    status                text        NOT NULL,
    plan                  text[]      NOT NULL DEFAULT '{}',
    idempotency_key       text        NULL,
    synthesis_cutoff_at   timestamptz NOT NULL,
    deadline_at           timestamptz NOT NULL,
    token_budget          bigint      NOT NULL,
    tokens_used           bigint      NOT NULL DEFAULT 0,
    error_code            text        NULL,
    warnings              text[]      NOT NULL DEFAULT '{}',
    gathering_end_reason  text        NULL,
    completed_at          timestamptz NULL,
    version               integer     NOT NULL DEFAULT 0,
    created_at            timestamptz NOT NULL,
    created_by            text        NOT NULL,
    updated_at            timestamptz NULL,
    updated_by            text        NULL
);

-- No tenancy (D20): the key is unique across all jobs.
CREATE UNIQUE INDEX IF NOT EXISTS ux_research_jobs_idempotency_key
    ON research_jobs (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

-- JobDeadlineWorker's two hot queries.
CREATE INDEX IF NOT EXISTS ix_research_jobs_active_deadline
    ON research_jobs (deadline_at)
    WHERE status IN ('Planning', 'Gathering', 'Synthesizing');

CREATE INDEX IF NOT EXISTS ix_research_jobs_gathering_cutoff
    ON research_jobs (synthesis_cutoff_at)
    WHERE status = 'Gathering';

CREATE TABLE IF NOT EXISTS job_steps (
    id               uuid        PRIMARY KEY,
    job_id           uuid        NOT NULL REFERENCES research_jobs (id),
    kind             text        NOT NULL,
    step_key         text        NOT NULL,
    status           text        NOT NULL,
    attempts         integer     NOT NULL DEFAULT 1,
    lease_until      timestamptz NOT NULL,
    last_error_code  text        NULL,
    trace_id         text        NULL,
    request_type     text        NOT NULL,
    request_payload  jsonb       NOT NULL,
    created_at       timestamptz NOT NULL,
    created_by       text        NOT NULL,
    updated_at       timestamptz NULL,
    updated_by       text        NULL,
    CONSTRAINT ux_job_steps_job_step_key UNIQUE (job_id, step_key)
);

-- StuckJobRecoveryWorker's hot query: running steps by lease.
CREATE INDEX IF NOT EXISTS ix_job_steps_running_lease
    ON job_steps (lease_until)
    WHERE status = 'Running';

CREATE TABLE IF NOT EXISTS research_reports (
    job_id      uuid        PRIMARY KEY REFERENCES research_jobs (id),
    content     text        NOT NULL,
    citations   jsonb       NOT NULL,
    model       text        NOT NULL,
    truncated   boolean     NOT NULL DEFAULT false,
    created_at  timestamptz NOT NULL,
    created_by  text        NOT NULL,
    updated_at  timestamptz NULL,
    updated_by  text        NULL
);
