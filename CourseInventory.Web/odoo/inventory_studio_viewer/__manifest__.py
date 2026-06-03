{
    "name": "Inventory Studio Viewer",
    "version": "1.0.0",
    "category": "Inventory",
    "summary": "Read-only viewer for Inventory Studio aggregate exports",
    "depends": ["base"],
    "data": [
        "security/ir.model.access.csv",
        "views/inventory_studio_views.xml",
    ],
    "application": True,
    "installable": True,
}
