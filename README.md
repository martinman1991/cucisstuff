# Cuci's Stuff – Desktop Kliens (WPF)

Magyar apróhirdetési platform asztali alkalmazása, amely a központi PHP+MySQL backenddel kommunikál egyéni API-n keresztül.  
A WPF-ben írt kliens a webes verzió funkcióinak nagy részét elérhetővé teszi natív asztali környezetben, azonban jelenleg **fejlesztési fázisban** van, számos hiányossággal.

---

## Funkciók

- **Bejelentkezés / regisztráció** – token-alapú API-hitelesítéssel, bcrypt jelszókezeléssel
- **Hirdetések böngészése** – `MainPage` kártyás nézetben, keresés a cím/leírás/eladó alapján (kliensoldali szűréssel)
- **Termék részletes nézet** – külön ablak (`ProductDetailWindow`) galériával, thumbnailekkel és lightbox-szal
- **Hirdetés feladása** – `UploadPage`, képek kiválasztása (több is), cím/leírás/ár megadása, képátméretezés kliensoldalon
- **Fiók kezelése** – `AccountPage`: felhasználónév, e-mail, jelszó módosítása; saját hirdetések listázása, elkeltnek jelölés, törlés
- **Admin felület** – `AdminPage`: füles nézet (termékek, felhasználók, rendelések, reportok) DataGrid-ben, VIZSGALOCK ki/be kapcsolás, VIZSGAPURGE
- **Üzenetküldés** – `MessagesPage`: partnerek listája, beszélgetések betöltése, üzenetküldés (automatikus frissítés nélkül)
- **Vásárlás** – `PurchasePage`: szállítási adatok megadása, rendelés leadása, a termék automatikusan elkeltnek jelölődik

---

## Technológiák

| Komponens           | Technológia                          |
|---------------------|--------------------------------------|
| Platform            | .NET Framework 4.7.2+ / .NET Core    |
| UI keretrendszer    | WPF (Windows Presentation Foundation)|
| Nyelv               | C#                                   |
| Kommunikáció        | HTTP kliens (`HttpClient`), saját API wrapper |
| Hitelesítés         | Token (`X-Api-Token` header)         |
| Adatok              | JSON (System.Text.Json)              |
| Képfeldolgozás      | WPF `BitmapImage`, `TransformedBitmap`, `JpegBitmapEncoder` |

---

## Jelenlegi hiányosságok (Desktop kliens)

A desktop alkalmazás számos ponton nem éri el a webes verzió funkcionalitását – az alábbi listában a legfontosabb ismert problémák és hiányzó funkciók szerepelnek.

### 1. Valós idejű üzenetküldés hiánya
Az `MessagesPage`-en nincs polling vagy automatikus frissítés. Az üzenetek csak manuálisan, a partner újbóli kiválasztásával frissíthetők. Nincs optimista UI, olvasási visszaigazolás, illetve valós idejű értesítés.

### 2. Képbetöltési probléma – termékmodal
A `ProductDetailWindow` a képeket **helyi fájlként** próbálja megnyitni (`File.Exists`, `Path.GetFullPath`). Mivel a képek a távoli webszerveren vannak (`https://cuci.local.pepa.hu/...`), ez a kód **soha nem fogja megjeleníteni a termék képeit**. A kártyák (`MainPage`) HTTP URL-t használnak, de a modal helytelenül dolgozik.

### 3. Kliensoldali keresés – nincs szerver oldali támogatás
A `MainPage` keresője csak a már letöltött elemek között szűr (LINQ). Hiányzik a valós idejű, szerveroldali AJAX keresés, ami a webes változatban megtalálható.

### 4. Nincs lapozás a főoldalon
A `MainPage` az **összes nem elkelt terméket** egyetlen kérésben letölti. Nagy termékszám esetén ez jelentős teljesítményromlást okozhat.

### 5. Admin oldal – hiányos funkcionalitás
- Nincs **lapozás vagy keresés** az adatgrid-ekben (minden rekord egyszerre betöltődik).
- A **VIZSGALOCK panel** csak ki/be kapcsolásra képes; nincs felület a kivételek hozzáadására/törlésére, és nem mutatja a jelenlegi állapotot (locked/unlocked). A webes adminban ez megvalósított.
- Nincs **termék részletes nézet** (product modal) az adminban – a termékadatokat nem lehet megtekinteni.
- Nincs **eladói profil** megjelenítés.

### 6. Saját termék vásárlása – hibás gombmegjelenítés
A `ProductDetailWindow` nem vizsgálja, hogy a bejelentkezett felhasználó **a termék eladója-e**. A "Vásárlás" gomb így a saját hirdetéseknél is megjelenik, és csak a `PurchasePage`-en derül ki, hogy a művelet nem engedélyezett.

### 7. Vásárlási konkurenciaprobléma
A `PurchasePage` a rendelés leadása előtt ugyan ellenőrzi a `sold` mezőt, de nem használ tranzakciót/zárolást – elméletileg előfordulhat, hogy egy terméket többször is megvásárolnak (race condition).

### 8. Jelszóváltoztatás – adatbázis inkonzisztencia
Az `AccountPage` jelszó módosításakor egy **új** sor kerül a `passwords` táblába, a régi hash viszont megmarad. Ha a felhasználó pontosan ugyanazt a jelszót adja meg, a `UNIQUE` constraint miatt hiba keletkezik, és a jelszó nem frissül.

### 9. Hiányzó témaváltás
A desktop kliens **csak sötét módot** használ (hardcode-oltan). Nincs világos mód, ellentétben a webes változattal.

### 10. Aszinkron holtpont veszélye
A `CheckVizsgalock()` szinkron metódus `Task.Run(…).GetAwaiter().GetResult()` hívást használ, ami **UI szálon deadlock-ot** okozhat.

### 11. Hibakezelés és visszajelzés
- Sok `try-catch` blokk **üres** (pl. `catch { return false; }`), ami lenyeli a hibákat.
- Nincs **betöltésjelző** (loading indicator) az API hívások idejére.
- A felhasználó felé a hibák legtöbbször csak `MessageBox`-ban jelennek meg, részletes információ nélkül.

### 12. Hiányzó képek az admin/rendelések nézetben
Az admin felület `DataGrid`-jei nem jelenítik meg a termékek képeit, és nem nyitható termékmodal. A megrendeléseknél (Orders) szintén nincs képmegjelenítés vagy részletes nézet.

---

## Telepítés (Desktop)

A desktop kliens egy WPF alkalmazás, amely a backend API-val kommunikál.  
A futtatáshoz szükséges:

1. **Backend** – a PHP/MySQL szerver (lásd a projekt webes részét) fut a megfelelő URL-en.
2. **API token** – a `MainWindow.xaml.cs`-ben található `API_TOKEN` értékének egyeznie kell a szerver `api.php`-ban beállított titkos tokennel.
3. **API URL** – az `API_URL` konstans mutasson a valós API végpontra (alapértelmezetten `http://cuci.local.pepa.hu/api.php`).

A projekt megnyitása Visual Studio-ban, a szükséges NuGet csomagok (pl. `System.Text.Json`) telepítése után fordítható és futtatható.

---

## Fejlesztési tervek

A fenti hiányosságok javítása folyamatos. A prioritások:
- HTTP-n keresztüli képbetöltés a termékmodalban
- Valós idejű üzenet polling
- Admin VIZSGALOCK kivételkezelés
- Szerveroldali keresés implementálása
- Témaváltás (világos mód) a WPF kliensben
- Hibakezelés és felhasználói visszajelzések fejlesztése

---

A desktop kliens **fejlesztői környezetben** működőképes, de a felsorolt hiányosságok miatt éles használatra még nem ajánlott.
