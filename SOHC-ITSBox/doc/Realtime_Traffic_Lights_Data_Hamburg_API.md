# Realtime Traffic Lights Data Hamburg API

<img src="file:///C:/Users/Paiman/Pictures/Marktext_MD_Bilder/2024-12-02-11-18-28-image.png" title="" alt="" width="455">

<img src="file:///C:/Users/Paiman/Pictures/Marktext_MD_Bilder/2024-12-02-11-29-32-image.png" title="" alt="" width="877">

### Koordinate der Ampel

Unter `value.<Ampelnummer>.observedArea.coordinates.0` findet man die Koordinate der Ampel.

<img title="" src="file:///C:/Users/Paiman/Pictures/Marktext_MD_Bilder/2024-12-02-11-40-23-image.png" alt="" width="589" data-align="center">

Diese kann man mit den Koordinaten der Ampeln aus unserem Testgebiet abgleichen ob dieses Datum relevant ist.

### Ampelphasen

Unter `value.<Ampelnummer>.unitOfMeasurement.definition` sind die Ampelphasen definiert. Jede Ampelphase hat eine Zahl.

<img title="" src="file:///C:/Users/Paiman/Pictures/Marktext_MD_Bilder/2024-12-02-11-36-47-image.png" alt="" width="835" data-align="center">

### Observations

Unter `value.<Ampelnummer>.Observations.<0 bis Anzahl an gewünschten Observations>` findet man alle Observations für diese Ampel. 

Die Anzahl an Observations kann man mit der REST Anfrage steuern.

Die beiden relevanten properties in den jeweiligen Observations sind `resultTime` und `result`.

**Zeitpunkt zu dem die Ampelphase gewechselt ist**

Die Property `resultTime` hat als value einen Timestamp der aussagt wann die Ampel zur nächsten Ampelphase gewechselt ist.

**Ampelphase**   

Die Property `result` hält als value eine Zahl, die für die Ampelphase steht.  

![](C:\Users\Paiman\Pictures\Marktext_MD_Bilder\2024-12-02-11-54-26-image.png)


