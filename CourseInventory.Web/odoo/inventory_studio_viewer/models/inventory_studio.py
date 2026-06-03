import requests

from odoo import _, api, fields, models
from odoo.exceptions import UserError


class InventoryStudioImport(models.Model):
    _name = "inventory.studio.import"
    _description = "Inventory Studio Import"

    name = fields.Char(required=True)
    source_url = fields.Char(required=True)
    api_token = fields.Char(required=True)
    imported_at = fields.Datetime(readonly=True)
    field_ids = fields.One2many("inventory.studio.field", "inventory_id", readonly=True)
    aggregate_ids = fields.One2many("inventory.studio.aggregate", "inventory_id", readonly=True)

    def action_import_results(self):
        for record in self:
            record._import_results()
        return True

    def _import_results(self):
        self.ensure_one()

        if not self.source_url or not self.api_token:
            raise UserError(_("Source URL and API token are required."))

        try:
            response = requests.get(
                self.source_url,
                params={"token": self.api_token},
                timeout=20,
            )
        except requests.RequestException as error:
            raise UserError(_("Could not reach Inventory Studio: %s") % error) from error

        if response.status_code in (401, 403):
            raise UserError(_("Inventory Studio rejected the API token."))

        if response.status_code == 404:
            raise UserError(_("Inventory Studio aggregate endpoint was not found."))

        if not response.ok:
            raise UserError(_("Inventory Studio returned HTTP %s.") % response.status_code)

        try:
            data = response.json()
        except ValueError as error:
            raise UserError(_("Inventory Studio returned invalid JSON.")) from error

        self.name = data.get("inventoryTitle") or self.name
        self.imported_at = fields.Datetime.now()
        self.field_ids.sudo().unlink()
        self.aggregate_ids.sudo().unlink()

        field_rows = []
        for field in data.get("fields", []):
            field_rows.append({
                "inventory_id": self.id,
                "title": field.get("title") or _("Untitled"),
                "field_type": field.get("type") or _("Unknown"),
            })
        self.env["inventory.studio.field"].sudo().create(field_rows)

        aggregate_rows = []
        for aggregate in data.get("numericAggregates", []):
            field_title = aggregate.get("field") or _("Unknown")
            for aggregate_type in ("min", "max", "average"):
                value = aggregate.get(aggregate_type)
                if value is not None:
                    aggregate_rows.append({
                        "inventory_id": self.id,
                        "field_title": field_title,
                        "aggregate_type": aggregate_type,
                        "value": str(value),
                        "count": 0,
                    })

        for aggregate in data.get("textAggregates", []):
            field_title = aggregate.get("field") or _("Unknown")
            for value in aggregate.get("values", []):
                aggregate_rows.append({
                    "inventory_id": self.id,
                    "field_title": field_title,
                    "aggregate_type": "popular_value",
                    "value": value.get("value") or "",
                    "count": int(value.get("count") or 0),
                })

        self.env["inventory.studio.aggregate"].sudo().create(aggregate_rows)


class InventoryStudioField(models.Model):
    _name = "inventory.studio.field"
    _description = "Inventory Studio Field"
    _order = "title"

    inventory_id = fields.Many2one("inventory.studio.import", required=True, ondelete="cascade")
    title = fields.Char(required=True, readonly=True)
    field_type = fields.Char(required=True, readonly=True)


class InventoryStudioAggregate(models.Model):
    _name = "inventory.studio.aggregate"
    _description = "Inventory Studio Aggregate"
    _order = "field_title, aggregate_type, count desc"

    inventory_id = fields.Many2one("inventory.studio.import", required=True, ondelete="cascade")
    field_title = fields.Char(required=True, readonly=True)
    aggregate_type = fields.Selection(
        [
            ("min", "Minimum"),
            ("max", "Maximum"),
            ("average", "Average"),
            ("popular_value", "Popular value"),
        ],
        required=True,
        readonly=True,
    )
    value = fields.Char(readonly=True)
    count = fields.Integer(readonly=True)
