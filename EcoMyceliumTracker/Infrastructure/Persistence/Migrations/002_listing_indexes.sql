-- Indexes for the listing queries. Measured on 200k networks before adding
-- them: page 1 of the unfiltered listing took 17 ms, the ILIKE filter 54 ms
-- and its COUNT(*) 97 ms, every one of them a sequential scan over the whole
-- table.

-- The listing orders by created_at DESC, id. Without a matching index
-- PostgreSQL reads and sorts every row to return twenty of them.
CREATE INDEX IF NOT EXISTS ix_mycelium_networks_created_at
    ON mycelium_networks(created_at DESC, id);

-- The name and soil filters use ILIKE '%term%'. A leading wildcard makes a
-- btree index useless, so these need trigram indexes. pg_trgm ships with
-- PostgreSQL but is not enabled by default, and creating an extension needs a
-- privilege a managed instance may withhold. Failing here would stop the
-- application from starting over an optimisation, so it degrades instead: the
-- filters keep working, they just fall back to a sequential scan.
DO $$
BEGIN
    BEGIN
        CREATE EXTENSION IF NOT EXISTS pg_trgm;
    EXCEPTION WHEN insufficient_privilege OR feature_not_supported THEN
        RAISE NOTICE 'pg_trgm unavailable; skipping the trigram indexes.';
        RETURN;
    END;

    CREATE INDEX IF NOT EXISTS ix_mycelium_networks_scientific_name_trgm
        ON mycelium_networks USING gin (scientific_name gin_trgm_ops);

    CREATE INDEX IF NOT EXISTS ix_mycelium_networks_soil_type_trgm
        ON mycelium_networks USING gin (soil_type gin_trgm_ops);
END $$;
