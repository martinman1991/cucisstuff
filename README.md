# cucisstuff - Online Piactér Platform

Egy funkciókban gazdag magyar online piactér webalkalmazás valós idejű üzenetküldéssel, admin felülettel és két téma támogatással.

## 📋 Áttekintés

A **cucisstuff** egy komplett apróhirdetési platform, ahol a felhasználók termékeket hirdethetnek meg, valós idejű üzeneteket válthatnak, kezelhetik hirdetéseiket és kapcsolatba léphetnek az eladókkal. A platform tartalmaz egy adminisztrációs felületet moderációs képességekkel, jelentéskezeléssel és rendszerkarbantartó eszközökkel.

## ✨ Funkciók

### 👤 Felhasználói funkciók
- **Hitelesítési rendszer** - Biztonságos bejelentkezés/regisztráció jelszó hasheléssel
- **Termék hirdetések** - Hirdetések létrehozása, szerkesztése és törlése több kép feltöltésével (JPEG, PNG, GIF, WebP, max 5MB/kép)
- **Képgaléria** - Fő kép kiválasztása és bélyegkép navigáció
- **Keresés** - AJAX-alapú keresés a termékcímekben és leírásokban
- **Felhasználói profilok** - Eladói profilok megtekintése hirdetéseikkel és statisztikáikkal
- **Reszponzív design** - Adaptív rács elrendezés minden eszközön (asztali gép, tablet, mobil)

### 💬 Üzenetküldő rendszer
- **Valós idejű üzenetek** - AJAX polling az azonnali üzenetkézbesítésért
- **Beszélgetéskezelés** - Csevegési előzmények, olvasatlan üzenet jelzők
- **Üzenet műveletek** - Saját üzenetek szerkesztése, törlése, nem megfelelő üzenetek jelentése
- **Toast értesítések** - Vizuális figyelmeztetések új üzenetekhez
- **Olvasási visszaigazolás** - Dupla pipa jelzés az olvasott üzeneteknél

### 🔧 Admin funkciók
- **Admin irányítópult** - Rendszerstatisztikák és gyors navigáció
- **Felhasználókezelés** - Felhasználók megtekintése, szerkesztése és törlése hirdetésszámokkal
- **Hirdetéskezelés** - Bármely hirdetés szerkesztése vagy eltávolítása, részletek megtekintése
- **Jelentésrendszer** - Felhasználói jelentések kezelése termékekre és üzenetekre

### 🎨 Téma rendszer
- **Sötét/Világos módok** - Váltás sötét (narancs) és világos (zöld) témák között
- **Preferencia mentés** - A téma választás localStorage-ben tárolódik
- **CSS változók** - Könnyű téma testreszabás

## 🛠️ Technológiai stack

| Összetevő | Technológia |
|-----------|-------------|
| Backend | PHP 7.4+ (PDO adatbázishoz) |
| Adatbázis | MySQL / MariaDB |
| Frontend | HTML5, CSS3, JavaScript (ES6) |
| Stílusok | Egyedi CSS üvegmorfológiai effektekkel |
| AJAX | Fetch API valós idejű funkciókhoz |
| Képek | PHP GD (fájlfeltöltésen keresztül) |
| Hitelesítés | Session-alapú bcrypt jelszó hasheléssel |
| Helyi szerver | XAMPP / WAMP / MAMP |

## 📦 Telepítés

### Követelmények
- **XAMPP** (vagy WAMP/MAMP) PHP 7.4+ és MySQL tartalommal
- PHP kiterjesztések: PDO, MySQLi, GD, fileinfo
- JavaScript-et támogató webböngésző
- **Git**

### Lépésről lépésre telepítés (XAMPP)

#### 1. XAMPP elindítása
Indítsd el a XAMPP Vezérlőpultot. Kattints az **Apache** és a **MySQL** melletti **Start** gombokra. Győződj meg róla, hogy mindkét szolgáltatás zöld háttérrel fut.

#### 2. Projekt letöltése a htdocs mappába
Nyiss egy parancssort (CMD, PowerShell vagy Git Bash), majd navigálj el a XAMPP telepítési könyvtárán belül a `htdocs` mappába.

```bash
git clone https://github.com/martinman1991/cucisstuff

### 3. Adatbázis importálása
Nyisd meg a böngésződben a phpMyAdmin felületet: [http://localhost/phpmyadmin](http://localhost/phpmyadmin)

- Kattints az **Új adatbázis** gombra (bal oldali menüben)
- Adj neki egy nevet (pl. `cucisstuff_db`), karakterkészletnek hagyd `utf8mb4_general_ci` értéken
- Kattints a **Létrehozás** gombra
- A frissen létrehozott adatbázisban kattints az **SQL** fülre
- Nyisd meg a projektben található `db.sql` fájlt, másold ki a teljes tartalmát
- Illeszd be a phpMyAdmin SQL szövegmezőjébe, majd kattints az **Indítás** gombra

### 4. Konfigurációs fájl beállítása
A projekt gyökérkönyvtárában keresd meg a konfigurációs fájlt (pl. `config.php`, `.env`, `includes/config.php`). Nyisd meg egy szövegszerkesztővel (pl. Notepad++, VS Code).

Állítsd be az adatbázis kapcsolat paramétereit az alábbiak szerint:

```php
define('DB_HOST', 'localhost');
define('DB_NAME', 'cucisstuff_db');   // a korábban létrehozott adatbázis neve
define('DB_USER', 'root');            // XAMPP alapértelmezett felhasználó
define('DB_PASS', '');                // XAMPP alapértelmezett jelszó (üres)

### 5. Webalkalmazás megnyitása
Nyisd meg a böngészőt, és navigálj a következő címre:
http://localhost/cucisstuff

Ha minden jól működik, a cucisstuff kezdőlapja fogad.

### 🧪 Hibaelhárítási tippek

| Hiba | Megoldás |
|------|----------|
| **404 Not Found** | Ellenőrizd, hogy a projekt mappa neve helyes-e (`cucisstuff`), és hogy a fájlok valóban a `htdocs` alatt vannak. |
| **Adatbázis kapcsolódási hiba** | Nézd át a konfigurációs fájl beállításait, és győződj meg róla, hogy a MySQL fut a XAMPP-ban. |
| **Üres oldal / PHP hibák** | Kapcsold be a PHP hibajelentést a `php.ini` fájlban, vagy a projekt elején add hozzá: `error_reporting(E_ALL); ini_set('display_errors', 1);` |
| **Feltöltés nem működik** | Ellenőrizd a `php.ini` `upload_max_filesize` és `post_max_size` értékeit (a projekt max 5MB-os képeket enged). |

## 📁 Projekt struktúra
cucisstuff/
├── admin/ # Admin felület fájljai
├── assets/ # Statikus fájlok (CSS, JS, képek)
│ ├── css/
│ ├── js/
│ └── images/
├── includes/ # PHP segédfüggvények és konfigurációk
│ ├── config.php
│ ├── functions.php
│ └── auth.php
├── uploads/ # Felhasználók által feltöltött képek
├── database/ # SQL séma fájlok
│ └── cucisstuff.sql
├── index.php # Kezdőlap
├── product.php # Termék részletek oldal
├── profile.php # Felhasználói profil oldal
├── messages.php # Üzenetküldő felület
├── login.php # Bejelentkezés
├── register.php # Regisztráció
├── logout.php # Kijelentkezés
└── README.md # Ez a dokumentáció

## 🤝 Közreműködés

A projekt fejlesztés alatt áll. Ha hibát találsz vagy fejlesztési javaslatod van, nyiss egy issue-t a GitHub repository-ban.
