-- Run this in pgAdmin while connected as your PostgreSQL administrator user.
-- It creates a dedicated database user for the ASP.NET Core application.

CREATE USER course_inventory WITH PASSWORD 'YOUR_LOCAL_PASSWORD';
CREATE DATABASE course_inventory OWNER course_inventory;
GRANT ALL PRIVILEGES ON DATABASE course_inventory TO course_inventory;
