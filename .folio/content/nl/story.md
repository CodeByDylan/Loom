# Verhaal

Elke nieuwe .NET-service begon op dezelfde manier. Een `Result`-type, net iets anders
geschreven dan de vorige keer. Een mappenstructuur waar opnieuw over werd gediscussieerd.
De vraag of "niet gevonden" nu een exception is of een retourwaarde, opnieuw beantwoord en
opnieuw anders.

Geen van die dingen is een moeilijk probleem. Juist daarom is het zonde om ze telkens
opnieuw te beslissen — de kosten zitten niet in het nadenken, maar in het feit dat twee
services binnen dezelfde solution het oneens blijken over hoe een fout eruitziet.

Loom neemt die beslissingen dus weg. Er is één `Error`, met een vaste code en één van zes
categorieën, en die categorie bepaalt de statuscode, het logniveau en of opnieuw proberen
zinvol is. Een endpoint wordt daarmee een dunne adapter zonder eigen vertaallogica.
Handlers worden geïnjecteerd en direct aangeroepen, zodat "go to definition" bij de handler
uitkomt in plaats van bij een registry.

De lastigere helft was bepalen wat er níét in moest. Geen mediator, want indirectie die je
niet kunt volgen kost je bij elke keer lezen. Geen repository, want `DbContext` is al een
unit of work, en het inpakken ervan doet de `IQueryable`-compositie teniet waar
specifications juist op leunen. Geen optie om de inhoud van een request te loggen — niet
standaard uitgeschakeld, maar afwezig, want een optie is een uitnodiging.

Tien pakketten in plaats van één, zodat niets een framework binnenhaalt waar je niet om
hebt gevraagd. Ze worden in lockstep geversioneerd, omdat een matrix van compatibele
versies zelf weer onderhoud oplevert — en dit is bedoeld om dat soort onderhoud weg te
nemen, niet om er iets aan toe te voegen.