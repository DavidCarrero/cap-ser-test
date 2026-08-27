-- Sample data for local development only. Do not run against a real environment.

INSERT INTO customers (full_name, document_number, country_code) VALUES
    ('Ana Quispe',        '1234567',   'PE'),
    ('Bruno Ferrer',      'X9081726',  'ES'),
    ('Claudia Restrepo',  '52889174',  'CO'),
    ('Daniel Meier',      '756.1234',  'CH')
ON CONFLICT (country_code, document_number) DO NOTHING;

INSERT INTO fx_rates (base_code, quote_code, rate, as_of) VALUES
    ('USD', 'CHF', 0.79240000, now() - interval '1 day'),
    ('USD', 'CHF', 0.79515000, now()),
    ('EUR', 'CHF', 0.93180000, now())
ON CONFLICT (base_code, quote_code, as_of) DO NOTHING;

INSERT INTO transactions (customer_id, amount, currency_code, created_at, fx_rate_id)
SELECT c.id, v.amount, v.currency_code, now() - v.age, f.id
FROM (VALUES
        ('1234567',  'PE', 1500.0000::numeric, 'USD', interval '3 days'),
        ('1234567',  'PE',  275.5000::numeric, 'USD', interval '2 days'),
        ('X9081726', 'ES', 8400.2500::numeric, 'EUR', interval '5 days'),
        ('52889174', 'CO',  990.0000::numeric, 'USD', interval '1 day'),
        ('756.1234', 'CH', 12000.000::numeric, 'CHF', interval '6 hours')
     ) AS v (document_number, country_code, amount, currency_code, age)
JOIN customers c
  ON c.document_number = v.document_number AND c.country_code = v.country_code
LEFT JOIN LATERAL (
    SELECT id FROM fx_rates
    WHERE base_code = v.currency_code AND quote_code = 'CHF'
    ORDER BY as_of DESC LIMIT 1
) f ON true;
