CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS tenants (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now())
);

CREATE TABLE IF NOT EXISTS work_items (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    title VARCHAR(250) NOT NULL,
    description TEXT NULL,
    status VARCHAR(50) NOT NULL,
    priority VARCHAR(50) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
    created_by VARCHAR(100) NOT NULL,
    updated_by VARCHAR(100) NOT NULL,
    CONSTRAINT ck_work_items_status CHECK (status IN ('New', 'InProgress', 'Blocked', 'Done', 'Cancelled')),
    CONSTRAINT ck_work_items_priority CHECK (priority IN ('Low', 'Medium', 'High', 'Critical'))
);

CREATE INDEX IF NOT EXISTS ix_work_items_tenant_id_status_created_at
    ON work_items (tenant_id, status, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS work_item_history (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id),
    work_item_id UUID NOT NULL REFERENCES work_items(id),
    action VARCHAR(100) NOT NULL,
    from_status VARCHAR(50) NULL,
    to_status VARCHAR(50) NULL,
    changed_by VARCHAR(100) NOT NULL,
    correlation_id VARCHAR(100) NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now())
);

CREATE INDEX IF NOT EXISTS ix_work_item_history_tenant_work_item_created
    ON work_item_history (tenant_id, work_item_id, created_at_utc DESC);

CREATE OR REPLACE FUNCTION sp_work_items_bulk_transition(
    p_tenant_id UUID,
    p_work_item_ids UUID[],
    p_target_status VARCHAR(50),
    p_changed_by VARCHAR(100),
    p_correlation_id VARCHAR(100)
)
RETURNS TABLE(
    updated_count INTEGER,
    rejected_count INTEGER
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_input_count INTEGER := COALESCE(array_length(p_work_item_ids, 1), 0);
    v_updated_count INTEGER := 0;
BEGIN
    IF p_tenant_id IS NULL THEN
        RAISE EXCEPTION 'p_tenant_id is required';
    END IF;

    IF p_target_status NOT IN ('New', 'InProgress', 'Blocked', 'Done', 'Cancelled') THEN
        RAISE EXCEPTION 'p_target_status value is invalid: %', p_target_status;
    END IF;

    IF v_input_count = 0 THEN
        RETURN QUERY SELECT 0, 0;
        RETURN;
    END IF;

    WITH input_ids AS (
        SELECT UNNEST(p_work_item_ids) AS id
    ),
    candidates AS (
        SELECT wi.id, wi.status
        FROM work_items wi
        INNER JOIN input_ids i ON i.id = wi.id
        WHERE wi.tenant_id = p_tenant_id
    ),
    eligible AS (
        SELECT c.id, c.status
        FROM candidates c
        WHERE c.status NOT IN ('Done', 'Cancelled')
          AND c.status <> p_target_status
    ),
    updated AS (
        UPDATE work_items wi
        SET
            status = p_target_status,
            updated_at_utc = timezone('utc', now()),
            updated_by = p_changed_by
        FROM eligible e
        WHERE wi.id = e.id
        RETURNING wi.id, e.status AS from_status
    ),
    history AS (
        INSERT INTO work_item_history (
            id,
            tenant_id,
            work_item_id,
            action,
            from_status,
            to_status,
            changed_by,
            correlation_id,
            created_at_utc
        )
        SELECT
            gen_random_uuid(),
            p_tenant_id,
            u.id,
            'bulk-transition',
            u.from_status,
            p_target_status,
            p_changed_by,
            p_correlation_id,
            timezone('utc', now())
        FROM updated u
        RETURNING 1
    )
    SELECT COUNT(*)
    INTO v_updated_count
    FROM updated;

    RETURN QUERY
    SELECT
        v_updated_count,
        GREATEST(v_input_count - v_updated_count, 0);
END;
$$;
