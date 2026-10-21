BEGIN IMMEDIATE;
CREATE TABLE "audit" (
"when" TEXT,
  "action" TEXT,
  "detail" TEXT
);
CREATE TABLE catalog_items (catalog_type TEXT NOT NULL, name TEXT NOT NULL, price REAL NOT NULL, sort_order INTEGER DEFAULT 0, category TEXT DEFAULT 'عام', image_path TEXT, barcode TEXT, stock_qty REAL, low_stock_threshold REAL DEFAULT 5, PRIMARY KEY(catalog_type, name));
CREATE TABLE categories (name TEXT PRIMARY KEY, sort_order INTEGER DEFAULT 0, station TEXT DEFAULT '');
CREATE TABLE "companies" (
"company_id" INTEGER PRIMARY KEY,
  "name" TEXT,
  "phone" TEXT,
  "notes" TEXT
);
CREATE TABLE "customers" (
"name" TEXT,
  "phone" TEXT,
  "address" TEXT,
  "notes" TEXT
);
CREATE TABLE "drivers" (
"driver_id" INTEGER PRIMARY KEY,
  "name" TEXT,
  "phone" TEXT,
  "notes" TEXT
);
CREATE TABLE "expenses" (
"shift_id" INTEGER,
  "who" TEXT,
  "when" TEXT,
  "amount_egp" REAL,
  "note" TEXT
);
CREATE TABLE item_options (option TEXT PRIMARY KEY, sort_order INTEGER DEFAULT 0);
CREATE TABLE item_variants (catalog_type TEXT NOT NULL, item_name TEXT NOT NULL, variant_name TEXT NOT NULL, price REAL NOT NULL, sort_order INTEGER DEFAULT 0, PRIMARY KEY(catalog_type, item_name, variant_name));
CREATE TABLE "orders" (
"order_id" INTEGER,
  "shift_id" INTEGER,
  "shift_start_at" TEXT,
  "date" TEXT,
  "hour" TEXT,
  "created_at" TEXT,
  "order_type" TEXT,
  "dine_subtype" TEXT,
  "items_json" TEXT,
  "discount_egp" REAL,
  "delivery_fee_egp" REAL,
  "payment_method" TEXT,
  "paid" REAL,
  "total" REAL,
  "customer_phone" TEXT,
  "customer_name" TEXT,
  "customer_address" TEXT,
  "customer_notes" TEXT,
  "order_notes" TEXT,
  "company_name" TEXT,
  "driver_id" INTEGER,
  "driver_name" TEXT,
  "driver_phone" TEXT,
  "notes_options_json" TEXT,
  "driver_notified" TEXT,
  "status" TEXT,
  "cashier_name" TEXT,
  "table_no" INTEGER
);
CREATE TABLE restaurant_tables (table_no INTEGER PRIMARY KEY, label TEXT, status TEXT DEFAULT 'free');
CREATE TABLE "session" (
"key" TEXT PRIMARY KEY,
  "value" TEXT
);
CREATE TABLE shift_batch_archives (archive_key TEXT NOT NULL, shift_id TEXT NOT NULL, start_at TEXT, end_at TEXT, orders_count INTEGER, items_count INTEGER, gross_sales REAL, discounts_total REAL, delivery_fee_total REAL, net_after_discount REAL, vault_total REAL, net_after_vault REAL, PRIMARY KEY(archive_key, shift_id));
CREATE TABLE "shifts" (
"shift_id" INTEGER PRIMARY KEY,
  "start_at" TEXT,
  "end_at" TEXT,
  "status" TEXT
);
CREATE TABLE users (user_id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE, pin_salt TEXT NOT NULL, pin_hash TEXT NOT NULL, pin_iterations INTEGER NOT NULL, role TEXT DEFAULT 'cashier', active INTEGER DEFAULT 1);

CREATE UNIQUE INDEX idx_customers_phone ON customers(phone);
CREATE INDEX idx_customers_name ON customers(name);
CREATE UNIQUE INDEX idx_order_identity ON orders(shift_id,order_id);
CREATE INDEX idx_orders_date ON orders(date);
CREATE INDEX idx_orders_status_shift ON orders(status,shift_id);
CREATE INDEX idx_catalog_barcode ON catalog_items(barcode);
CREATE TABLE pos_schema(version INTEGER NOT NULL);
INSERT INTO pos_schema VALUES(1);
CREATE TABLE save_tokens(token TEXT PRIMARY KEY,shift_id INTEGER NOT NULL,order_id INTEGER NOT NULL);
INSERT INTO shifts(shift_id,start_at,end_at,status) VALUES(1,datetime('now','localtime'),'','open');
INSERT INTO session(key,value) VALUES('last_order_id','0');
COMMIT;
