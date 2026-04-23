# Cuci's Stuff — Online Piactér Platform

Magyar apróhirdetési platform valós idejű üzenetküldéssel, admin terminállal és kétféle témával. PHP + MySQL alapú, lokálisan futtatható XAMPP/WAMP/MAMP segítségével.

---

## Funkciók

### Felhasználói funkciók (`index.php`, `main.php`, `account.php`)
- **Bejelentkezés / regisztráció** — session-alapú hitelesítés bcrypt jelszóhasheléssel (`PASSWORD_DEFAULT`); külön `passwords` tábla a hash-eknek
- **Hirdetés feladása** — cím, leírás, ár + több kép feltöltése (`JPEG`, `PNG`, `GIF`, `WebP`, max 5 MB/kép); képek az `uploads/<item_id>/` könyvtárba kerülnek
- **Hirdetések böngészése** — véletlenszerű sorrendű rács (oldalonként 24 termék), lapozással
- **Termék modal** — teljes képernyős nézet galériával, lightboxszal, eladói profillal, szerkesztéssel és törléssel
- **AJAX-keresés** — valós idejű keresés cím és leírás alapján, legördülő eredménylistával
- **Eladói profil popup** — teljes képernyős nézet az eladó statisztikáival és legutóbbi hirdetéseivel
- **Fiók szerkesztése** — felhasználónév, e-mail és jelszó módosítása AJAX-on keresztül; saját hirdetések kezelése
- **Bejelentés** — hirdetések és üzenetek bejelentése moderálásra

### Üzenetküldő rendszer (`uzenetek.php`)
- **Valós idejű chat** — AJAX polling 500 ms-es intervallummal (háttérben 3 s), időbélyeg alapú szinkronizációval
- **Optimista UI** — az elküldött üzenet azonnal megjelenik, a szerver válasz után cserélődik a valós ID-ra
- **Sidebar frissítés** — a partnerek listája 5 másodpercenként frissül AJAX-on keresztül
- **Üzenet műveletek** — szerkesztés és törlés saját üzenetekre; bejelentés a fogadott üzenetekre
- **Új beszélgetés** — modálból indítható azokkal a felhasználókkal, akikkel még nincs aktív csevegés
- **Olvasási visszaigazolás** — ✓ / ✓✓ jelzés az elküldött üzeneteknél

### Admin felület (`admin.php`)
- **Katonai terminál stílus** — CRT scanlines, foszforeszcens zöld/amber szín, VT323 és Share Tech Mono betűtípus
- **Irányítópult** — felhasználók, hirdetések és reportok összesítése
- **Felhasználókezelés** — felhasználók listázása, szerkesztése, törlése; admin-státusz jelzése
- **Hirdetéskezelés** — bármely hirdetés szerkesztése vagy törlése (képfájlok törlésével együtt)
- **Reportok** — termék- és üzenetbejelentések kezelése, forrással együtt megtekinthető termékmodálban

### Témarendszer
- **Sötét mód** — narancs (`#ff9a1f`) akcentszín, üvegmorfológiai effektek
- **Világos mód** — zöld (`#B0CB1F`) akcentszín, krémszínű háttér
- A témaváltó `localStorage`-ban menti a beállítást; FOUC-megelőzés inline `<script>`-tel az `<head>`-ben

---

## Technológiai stack

| Összetevő | Technológia |
|---|---|
| Backend | PHP 7.4+ (PDO + MySQLi) |
| Adatbázis | MySQL / MariaDB |
| Frontend | HTML5, CSS3, JavaScript ES6 |
| AJAX | Fetch API (polling + optimistic UI) |
| Hitelesítés | Session + `password_hash()` / `password_verify()` |
| Képkezelés | PHP native `move_uploaded_file()` |
| Admin betűtípus | VT323, Share Tech Mono (Google Fonts) |
| Helyi szerver | XAMPP / WAMP / MAMP |

---

## Adatbázis-struktúra

```
passwords       id, password_hash (UNIQUE)
users           id, email (UNIQUE), username (UNIQUE), password_id (FK), created_at
admins          user_id (FK → users)
items           id CHAR(12), user_id (FK), title, description, price, created_at, updated_at
item_images     id, item_id (FK), image_path, image_filename, is_primary, sort_order, uploaded_at
reports         id, item_id (FK), user_id (FK), reason, status (pending/resolved/dismissed), created_at
uzenetek        id CHAR(25), sender_id (FK), receiver_id (FK), message, sent_at, is_read
message_reports id, message_id (FK → uzenetek), reporter_user_id (FK), reason, status, created_at
```

Az üzenet ID-k 25 karakteres, véletlenszerűen generált alfanumerikus stringek. A termék ID-k 12 karakteresek, ütközés-ellenőrzéssel szúródnak be.

---

## Telepítés

### Követelmények
- XAMPP (vagy WAMP / MAMP) PHP 7.4+ és MySQL tartalommal
- PHP kiterjesztések: `PDO`, `MySQLi`, `fileinfo`
- JavaScript-et támogató böngésző

### Lépések

**1. XAMPP elindítása**

Indítsd el a XAMPP Vezérlőpultot, és kattints az **Apache** és **MySQL** melletti **Start** gombra.

**2. Repo klónozása**

```bash
cd C:/xampp/htdocs
git clone https://github.com/martinman1991/cucisstuff
```

**3. Adatbázis létrehozása**

Nyisd meg a [phpMyAdmin](http://localhost/phpmyadmin) felületet, hozz létre egy új adatbázist (pl. `cuci_ady_pepa_hu`), majd importáld a `db.sql` fájl tartalmát az **SQL** fülön.

**4. Konfigurációs fájl beállítása**

Nyisd meg a `config.php` fájlt, és írd át az adatbázis-adatokat:

```php
define('DB_HOST', 'localhost');
define('DB_USER', 'root');         // XAMPP alapértelmezett
define('DB_PASS', '');             // XAMPP alapértelmezett (üres)
define('DB_NAME', 'cuci_ady_pepa_hu');
```

> **Fontos:** Ne töltsd fel éles adatbázis-jelszót nyilvános repóba. Vedd fel a `config.php`-t a `.gitignore`-ba.

**5. Megnyitás böngészőben**

Navigálj a [http://localhost/cucisstuff](http://localhost/cucisstuff) címre.

---

## Hibaelhárítás

| Hiba | Megoldás |
|---|---|
| **404 Not Found** | Ellenőrizd, hogy a mappa neve `cucisstuff`, és a fájlok a `htdocs` alatt vannak. |
| **DB kapcsolódási hiba** | Ellenőrizd a `config.php` konstansokat, és hogy a MySQL fut a XAMPP-ban. |
| **Üres oldal / PHP hiba** | Adj hozzá `error_reporting(E_ALL); ini_set('display_errors', 1);` sort az oldal tetejére. |
| **Feltöltés nem működik** | Ellenőrizd a `php.ini` `upload_max_filesize` és `post_max_size` értékeit (min. 5 MB). |
| **`message_reports` tábla hiányzik** | Az admin felület automatikusan létrehozza az első betöltéskor `CREATE TABLE IF NOT EXISTS`-szal. |

---

## Projekt struktúra

```
cucisstuff/
├── config.php          # DB konstansok (DB_HOST, DB_USER, DB_PASS, DB_NAME)
├── db.sql              # Teljes adatbázis-séma (CREATE TABLE + admin seed)
├── index.php           # Bejelentkezés / regisztráció
├── main.php            # Főoldal — hirdetésrács, feltöltés, keresés
├── account.php         # Fiókom — adatmódosítás, saját hirdetések
├── uzenetek.php        # Valós idejű üzenetküldő
├── admin.php           # Admin terminál (csak admin szerepkörrel)
├── theme-dark.css      # Sötét téma (narancs akcentszín)
├── theme-light.css     # Világos téma (zöld akcentszín)
└── uploads/            # Feltöltött képek (<item_id>/<filename>)
```

---

## Közreműködés

A projekt fejlesztés alatt áll. Ha hibát találsz vagy fejlesztési javaslatod van, nyiss egy [issue-t a GitHub repository-ban](https://github.com/martinman1991/cucisstuff/issues).
