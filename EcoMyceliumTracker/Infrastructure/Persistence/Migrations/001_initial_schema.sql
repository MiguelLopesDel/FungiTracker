CREATE TABLE IF NOT EXISTS mycelium_networks (
    id UUID PRIMARY KEY,
    scientific_name VARCHAR(200) NOT NULL,
    soil_type VARCHAR(100) NOT NULL,
    discovered_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS sensor_nodes (
    id UUID PRIMARY KEY,
    network_id UUID NOT NULL REFERENCES mycelium_networks(id) ON DELETE CASCADE,
    location POINT NOT NULL,
    moisture_level NUMERIC(5, 2) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS nutrient_transfers (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_node_id UUID NOT NULL REFERENCES sensor_nodes(id) ON DELETE CASCADE,
    target_node_id UUID NOT NULL REFERENCES sensor_nodes(id) ON DELETE CASCADE,
    carbon_amount_mg INTEGER NOT NULL,
    transferred_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS ix_sensor_nodes_network_id
    ON sensor_nodes(network_id);

CREATE INDEX IF NOT EXISTS ix_nutrient_transfers_source_node_id
    ON nutrient_transfers(source_node_id);

CREATE INDEX IF NOT EXISTS ix_nutrient_transfers_target_node_id
    ON nutrient_transfers(target_node_id);

CREATE INDEX IF NOT EXISTS ix_nutrient_transfers_carbon_amount_mg
    ON nutrient_transfers(carbon_amount_mg);

CREATE INDEX IF NOT EXISTS ix_nutrient_transfers_transferred_at
    ON nutrient_transfers(transferred_at DESC);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_sensor_nodes_moisture_level'
    ) THEN
        ALTER TABLE sensor_nodes
            ADD CONSTRAINT ck_sensor_nodes_moisture_level
            CHECK (moisture_level BETWEEN 0 AND 100) NOT VALID;
        ALTER TABLE sensor_nodes VALIDATE CONSTRAINT ck_sensor_nodes_moisture_level;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_nutrient_transfers_carbon_amount'
    ) THEN
        ALTER TABLE nutrient_transfers
            ADD CONSTRAINT ck_nutrient_transfers_carbon_amount
            CHECK (carbon_amount_mg > 0) NOT VALID;
        ALTER TABLE nutrient_transfers VALIDATE CONSTRAINT ck_nutrient_transfers_carbon_amount;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_nutrient_transfers_distinct_nodes'
    ) THEN
        ALTER TABLE nutrient_transfers
            ADD CONSTRAINT ck_nutrient_transfers_distinct_nodes
            CHECK (source_node_id <> target_node_id) NOT VALID;
        ALTER TABLE nutrient_transfers VALIDATE CONSTRAINT ck_nutrient_transfers_distinct_nodes;
    END IF;
END $$;
