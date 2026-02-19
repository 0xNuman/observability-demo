INSERT INTO tenants (id, name, is_active)
VALUES ('11111111-1111-1111-1111-111111111111', 'Acme Corp', TRUE)
ON CONFLICT (id) DO NOTHING;

INSERT INTO work_items (
    id,
    tenant_id,
    title,
    description,
    status,
    priority,
    created_by,
    updated_by
)
VALUES (
    '22222222-2222-2222-2222-222222222222',
    '11111111-1111-1111-1111-111111111111',
    'Bootstrap observability dashboard',
    'Initial seed item for local smoke tests.',
    'New',
    'High',
    'seed',
    'seed'
)
ON CONFLICT (id) DO NOTHING;
