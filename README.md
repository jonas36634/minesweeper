# Kernport Minesweeper

Deutschsprachiges Minesweeper für Desktop und Smartphone, entwickelt mit .NET 10 LTS und Blazor WebAssembly. Der veröffentlichte Ordner enthält eine statische Website. Züge, Hinweise und Speicherung laufen im Browser.

## Entwicklung

Im Repository mit dem in `global.json` angegebenen SDK:

```sh
dotnet restore Client --locked-mode
dotnet run --project Client --urls http://localhost:5174
```

In der Codex-Cloud zuerst `bash ops/setup-dotnet.sh` und `source ops/cloud-env.sh` ausführen. Die Installation prüft das SDK gegen den SHA-512-Wert aus Microsofts Release-Metadaten und verwendet die eingecheckten NuGet-Lockdateien.

## Regeln und Speicherung

Die Bibliothek `Engine` verarbeitet Aufdecken, Flächenöffnung, Flaggen und Akkorde. Der erste Klick sperrt die umgebenden neun Positionen (am Rand entsprechend weniger) für Minen. Der Solver vergleicht sichtbare Nachbarschaften, Teilmengen und die globale Minenzahl. Manuell gesetzte Flaggen zählen erst dann als Beweis, wenn der Solver die Mine selbst nachweisen kann. Eine Suche im Logikmodus hat ein Budget von 3,5 Sekunden. Beim Abbruch oder nach einem erfolglosen Versuch bleibt das Startfeld wählbar.

Die Tagesaufgabe verwendet das UTC-Datum im ISO-Format, FNV-1a über `kernport/v1/YYYY-MM-DD`, einen 32-Bit-LCG und partielles Fisher-Yates-Shuffling. Auf dem 16×16-Feld ist der Start in Zeile 9, Spalte 9. Generator und Speicherformat sind Version 1. Erhaltene Spielstände enthalten Spielgröße, Seed, Erstzug und Zugfolge. Beim Laden wird die Partie nachgespielt. Die Schlüssel `kernport.game1` und `kernport.stats1` bleiben über Releases stabil. Zukünftige Generatoränderungen benötigen einen eigenen Versionspfad, damit alte Partien mit dem ursprünglichen Generator weiterlaufen.

## Prüfungen und Release

```sh
bash ops/build.sh
mkdir -p /tmp/minesweeper-release
tar -xzf artifacts/minesweeper-wwwroot.tar.gz -C /tmp/minesweeper-release
python3 -m http.server 5173 --directory /tmp/minesweeper-release
```

Das Build-Skript führt die Engine-Tests aus und veröffentlicht in einem frischen Verzeichnis. Für die Browserprüfungen in einem zweiten Terminal mit Python, Playwright und Chromium:

```sh
python3 Tests/browser.py http://localhost:5173
```

Die Browserprüfung spielt einen klassischen Sieg, einen Logiksieg mit Hinweisen und einen Verlust. Sie prüft außerdem Tastatur, mobile Touch-Ereignisse, täglichen Seed, Speicherfehler und die Wiederaufnahme beendeter sowie laufender Partien.

## Deployment mit Caddy

Für einen bestehenden Linux-Webhost mit Caddy 2, erreichbaren Ports 80/443 und SSH-Schreibzugriff auf `/srv/minesweeper`:

1. In beiden DNS-Zonen den Namen `minesweeper` per A auf die öffentliche IPv4-Adresse des Webhosts richten. AAAA nur bei funktionierendem IPv6 setzen. Stellt der Hoster einen Zielhost bereit, kann für beide Subdomains stattdessen ein CNAME darauf zeigen.
2. Die beiden Site-Blöcke aus `ops/Caddyfile` in die bestehende Caddy-Konfiguration einbinden. Der Webserver benötigt Lesezugriff auf die Releases. Caddy bezieht und erneuert die Zertifikate beider Hostnamen.
3. Im Repository bauen und das Archiv auf den gewählten Host übertragen:

```sh
bash ops/build.sh
bash ops/deploy.sh user@webhost
```

Das Skript extrahiert jedes Release in ein separates Verzeichnis und schaltet den Symlink `current` atomar um. Bestehende Framework-Dateien bleiben für offene Tabs verfügbar. Nach Erstkonfiguration auf dem Server Caddys Gesamtkonfiguration prüfen und neu laden. Bei späteren Spielupdates genügt der Deployment-Befehl.

```sh
caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
curl -fsSI https://minesweeper.kernport.com/
curl -sSI 'https://minesweeper.kernport.de/style.css?probe=1'
python3 Tests/browser.py https://minesweeper.kernport.com
```

Erwartet werden gültiges HTTPS, Status 200 auf der Hauptdomain und eine permanente Weiterleitung auf denselben Pfad samt Query unter der .com-Adresse. Browserprüfungen verwenden isolierte lokale Profile.

## Deployment über GitHub Pages

`bash ops/build.sh && bash ops/pages.sh` überträgt die statische Anwendung mit `.nojekyll` und `CNAME` auf den Branch `gh-pages`. In den Pages-Einstellungen des Repositorys als Quelle „Deploy from a branch“, `gh-pages` und `/(root)` konfigurieren und die benutzerdefinierte Domain `minesweeper.kernport.com` setzen. Für diese Variante lautet der DNS-Eintrag `minesweeper.kernport.com. CNAME jonas36634.github.io.`. Nach erfolgreicher Domain- und Zertifikatsprüfung „Enforce HTTPS“ aktivieren. Für `minesweeper.kernport.de` wird beim bestehenden DNS-/Webanbieter eine HTTPS-fähige permanente Weiterleitung auf `https://minesweeper.kernport.com` eingerichtet, die Pfad und Query übernimmt. Ein CNAME allein bewirkt keine HTTP-Weiterleitung.

### STRATO für die beiden Spieladressen

- In der Domainverwaltung von `kernport.com` die Subdomain `minesweeper` auswählen bzw. anlegen. Für diesen Namen den CNAME auf `jonas36634.github.io.` setzen. In GitHub Pages ist `minesweeper.kernport.com` als eigene Domain eingetragen. Nach Ausstellung des Zertifikats HTTPS erzwingen.
- Für `minesweeper.kernport.de` muss der Dienst, der die Weiterleitung ausliefert, ein gültiges Zertifikat für genau diesen Namen haben. Bietet das bestehende STRATO-Paket HTTPS-Weiterleitungen mit Erhalt von Pfad und Query, eine permanente Weiterleitung auf `https://minesweeper.kernport.com` einrichten.
- Bei STRATO-Webspace mit Apache und SSL kann stattdessen die .de-Subdomain einem eigenen Verzeichnis zugeordnet werden. Die Datei `ops/strato-redirect/.htaccess` dort als `.htaccess` ablegen und das Zertifikat der .de-Subdomain zuordnen. Die Regel betrifft nur den Spiel-Host. Für diesen Weg zeigt die .de-Subdomain auf den STRATO-Webspace.
- Prüffall: Ein HTTPS-Aufruf von `/style.css?probe=1` auf der .de-Adresse muss mit Status 301 und `Location: https://minesweeper.kernport.com/style.css?probe=1` antworten. Auf der .com-Adresse muss dieselbe Ressource mit gültigem HTTPS und Status 200 laden.
