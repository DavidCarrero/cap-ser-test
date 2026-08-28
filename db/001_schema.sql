-- Northgate / capital-services — normalized schema (PostgreSQL 17)
--
-- Design notes
--  * Money is NUMERIC, never float. Binary floating point cannot represent 0.10
--    exactly and the error accumulates across a ledger.
--  * Surrogate keys are BIGINT: a transaction ledger outgrows INT's 2.1B ceiling.
--  * Timestamps are TIMESTAMPTZ so an instant is unambiguous across regions.
--  * countries/currencies are lookup tables so the free-text country_code and
--    currency columns become constrained references (this is the normalization:
--    transactions previously carried a repeated free-text customer_name).

BEGIN;

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Uniqueness is declared as unique indexes rather than table UNIQUE
-- constraints. Postgres enforces both identically (a UNIQUE constraint is
-- backed by a unique index anyway); indexes are the more flexible form and are
-- what the EF Core model's [Index(IsUnique = true)] maps to, which keeps the
-- model and this file byte-comparable.

-- ---------------------------------------------------------------- reference --

CREATE TABLE countries (
    code        char(2)     PRIMARY KEY,                 -- ISO 3166-1 alpha-2
    name        text        NOT NULL,
    CONSTRAINT countries_code_upper CHECK (code = upper(code))
);

CREATE TABLE currencies (
    code        char(3)     PRIMARY KEY,                 -- ISO 4217
    name        text        NOT NULL,
    minor_unit  smallint    NOT NULL DEFAULT 2,          -- decimal places (JPY = 0)
    CONSTRAINT currencies_code_upper CHECK (code = upper(code)),
    CONSTRAINT currencies_minor_unit_range CHECK (minor_unit BETWEEN 0 AND 4)
);

-- ---------------------------------------------------------------- customers --

CREATE TABLE customers (
    id               bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name        text        NOT NULL,
    document_number  text        NOT NULL,
    country_code     char(2)     NOT NULL REFERENCES countries (code),
    created_at       timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT customers_full_name_not_blank CHECK (btrim(full_name) <> ''),
    CONSTRAINT customers_document_not_blank  CHECK (btrim(document_number) <> '')
);

-- Supports the ILIKE '%name%' search: a leading wildcard cannot use a B-tree,
-- but GIN + trigrams can.
CREATE UNIQUE INDEX countries_name_key ON countries (name);

-- A document number is only unique within the country that issued it.
CREATE UNIQUE INDEX customers_document_unique_per_country
    ON customers (country_code, document_number);

CREATE INDEX customers_full_name_trgm ON customers USING gin (full_name gin_trgm_ops);
CREATE INDEX customers_country_code   ON customers (country_code);

-- ----------------------------------------------------------------- fx rates --

CREATE TABLE fx_rates (
    id          bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    base_code   char(3)        NOT NULL REFERENCES currencies (code),
    quote_code  char(3)        NOT NULL REFERENCES currencies (code),
    rate        numeric(18, 8) NOT NULL,
    as_of       timestamptz    NOT NULL,

    CONSTRAINT fx_rates_rate_positive CHECK (rate > 0),
    CONSTRAINT fx_rates_distinct_pair CHECK (base_code <> quote_code)
);

-- The unique index above already covers the "newest rate for this pair" lookup
-- (Postgres scans it backwards for ORDER BY as_of DESC), so no separate DESC
-- index is needed. quote_code gets its own index because it is an FK that the
-- composite does not lead with.
CREATE UNIQUE INDEX fx_rates_unique_quote ON fx_rates (base_code, quote_code, as_of);
CREATE INDEX fx_rates_quote_code ON fx_rates (quote_code);

-- ------------------------------------------------------------- transactions --

CREATE TABLE transactions (
    id             bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    customer_id    bigint         NOT NULL REFERENCES customers (id),
    amount         numeric(19, 4) NOT NULL,
    currency_code  char(3)        NOT NULL REFERENCES currencies (code),
    created_at     timestamptz    NOT NULL DEFAULT now(),

    -- The rate applied when the transaction settled. NULL until converted.
    -- This is a historical fact, not derivable from today's rate, so it is
    -- stored rather than recomputed -- storing it is not a normalization
    -- violation, storing amount * rate would be.
    fx_rate_id     bigint         NULL REFERENCES fx_rates (id),

    CONSTRAINT transactions_amount_nonzero CHECK (amount <> 0)
);

-- Serves both the customer filter and the ORDER BY created_at DESC page.
CREATE INDEX transactions_customer_created ON transactions (customer_id, created_at DESC);
CREATE INDEX transactions_created_at       ON transactions (created_at DESC);
CREATE INDEX transactions_currency_code    ON transactions (currency_code);
CREATE INDEX transactions_fx_rate_id      ON transactions (fx_rate_id);

COMMIT;
