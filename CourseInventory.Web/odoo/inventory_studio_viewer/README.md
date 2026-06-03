# Inventory Studio Viewer

Minimal Odoo module for importing Inventory Studio aggregate JSON through the protected API token.

## Install

1. Copy `inventory_studio_viewer` into an Odoo addons path.
2. Restart Odoo.
3. Update the Apps list.
4. Install `Inventory Studio Viewer`.

## Import

1. In Inventory Studio, open an inventory as owner/admin.
2. Open the `API` tab.
3. Generate/reset the API token and copy it immediately.
4. In Odoo, open `Inventory Studio > Imported Inventories`.
5. Create a record:
   - `Source URL`: `https://your-app/api/inventories/aggregates`
   - `API Token`: the token copied from Inventory Studio
6. Click `Import from Inventory Studio`.

## Demo Script

1. Generate a token in Inventory Studio.
2. Show that the API endpoint returns `401` without the token.
3. Import in Odoo with the token.
4. Open the form and show imported fields.
5. Open aggregates and show numeric min/max/average plus popular text values.
6. Explain that the Odoo viewer cannot create or edit Inventory Studio items.
