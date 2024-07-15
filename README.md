# coffee 
Ziele 2024:
- upgrade der bestand anwendung von dotnet 6 und dotnet 8 auf dotnet 9 mithilfe des upgrade tools
- Einbau von KI/ChatGPT
- Überabeitung der Seite mithilfe von Github Copilot
- DevSecOps Konzepte anwenden
- Aufbau einer CI/CD mit GitLab die auch die Docker einsetzt

was sonst noch so passiert ist
Infrastruktur von Gitea auf Gitlab umgezogen
=> CI/CD aufbauen
=> Gitlab muss auch wieder an Sonar angebunden werden usw
=> Docker Registry aufbauen welche dann auch automatisch wieder auf der NAS die Container updated... 


## Dast/sast tools linksammlung:
https://docs.gitlab.com/ee/user/application_security/dast/
https://deepsource.com/platform/self-hosted
https://forum.gitlab.com/t/self-hosted-add-sast-for-all-users-projects/53528

## SAST (Static Application Security Testing):

SonarQube: Ein Open-Source-Tool zur kontinuierlichen Codequalitätsanalyse und -überprüfung auf Sicherheitslücken.
Checkmarx: Ein kommerzielles Tool, das statische Code-Analysen durchführt und Sicherheitslücken identifiziert.
Fortify: Ein weiteres kommerzielles Tool für statische Code-Analysen und Sicherheitstests.


## DAST (Dynamic Application Security Testing):

OWASP ZAP (Zed Attack Proxy): Ein Open-Source-Tool für Penetrationstests und Sicherheitsüberprüfungen während der Laufzeit.
Burp Suite: Ein weiteres bekanntes Tool für Web-Anwendungssicherheitstests und Penetrationstests.

## Container Security:

Clair: Ein Open-Source-Tool für die Überprüfung von Sicherheitslücken in Docker-Containern.
Docker Bench for Security: Ein Skript, das Sicherheitsbest Practices für Docker-Installationen überprüft.
Anchore Engine: Ein Tool für die Sicherheitsanalyse und -prüfung von Container-Images.


Installieren also:
Owasp zap
Burp suite
Clair
https://github.com/quay/clair
Checkmarx/fortify
https://checkmarx.com/
https://fortifyapp.com/


Benutzer testuser und das Passwort testpassword bereitstellen