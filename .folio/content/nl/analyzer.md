# Een analyzer voor de ene fout die deze stijl makkelijk maakt

## situation

Fouten als waarde teruggeven in plaats van als exception betekent dat een weggegooide
`Result` stilzwijgend een genegeerde fout is. De compiler heeft er niets over te zeggen.

## task

Weggegooide resultaten opvangen tijdens het bouwen, zonder dat iemand iets hoeft te
installeren of aan te zetten.

## action

`LOOM0001` meegeleverd in `CodeByDylan.Loom.Results`, zodat de analyzer met het pakket
meekomt. Bewust weggooien blijft toegestaan door `_ =` te schrijven, waardoor die
bedoeling zichtbaar wordt tijdens review.

## result

De eerste run op bestaande code vond acht ongecontroleerde weggooiacties in de testsuite
van deze repository zelf.
