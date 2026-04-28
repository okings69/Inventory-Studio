-- Run this in pgAdmin as your PostgreSQL administrator user if the app says
-- "authentification par mot de passe echouee" for course_inventory.

ALTER USER course_inventory WITH PASSWORD 'YOUR_LOCAL_PASSWORD';
GRANT ALL PRIVILEGES ON DATABASE course_inventory TO course_inventory;
