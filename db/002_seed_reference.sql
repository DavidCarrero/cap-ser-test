-- Reference data. Idempotent: safe to re-run.

INSERT INTO countries (code, name) VALUES
    ('CH', 'Switzerland'),
    ('CO', 'Colombia'),
    ('DE', 'Germany'),
    ('ES', 'Spain'),
    ('GB', 'United Kingdom'),
    ('PE', 'Peru'),
    ('US', 'United States')
ON CONFLICT (code) DO NOTHING;

INSERT INTO currencies (code, name, minor_unit) VALUES
    ('CHF', 'Swiss Franc',        2),
    ('COP', 'Colombian Peso',     2),
    ('EUR', 'Euro',               2),
    ('GBP', 'Pound Sterling',     2),
    ('JPY', 'Yen',                0),
    ('PEN', 'Sol',                2),
    ('USD', 'US Dollar',          2)
ON CONFLICT (code) DO NOTHING;
