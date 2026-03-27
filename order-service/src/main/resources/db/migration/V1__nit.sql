create table if not exists db_connection_test (
  id serial primary key,
  created_at timestamptz not null default now()
);
