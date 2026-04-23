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
- **Beszélgetés nézegető** - Felhasználói beszélgetések monitorozása (csak admin)

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

### Lépésről lépésre telepítés (XAMPP)

1. **Repo klónozása vagy letöltése**
   ```bash
   git clone https://github.com/martinman1991/cucisstuff
