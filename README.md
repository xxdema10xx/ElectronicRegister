PRIMA COSA DA FARE QUANDO SI INIZIA A LAVORARE SUL PROGETTO:

(dovunque scrivo terminale intendo terminale di vs code)

git pull --no-edit   (così scarico le modifiche fatte dagli altri, sennò lavorate su una versione vecchia del progetto)


COMANDO PER AVVIARE L'API CON PORTA HTTPS 

-spostarsi da terminale con cd Backend/ElectronicRegisterAPI, necessario usare terminale a parte

dotnet run --urls "https://localhost:7225;http://localhost:5257"


COMANDI PER GITHUB: (dalla root del progetto [ElectronicRegister])

git add .

git commit -m "MESSAGGIO"

git pull --no-edit

git push


PER REACT: 

-spostarsi da terminale con cd Frontend/ElectronicRegisterReact, necessario usare terminale a parte

npx expo start


SE NON HAI IL PROGETTO SUL TUO PC:

-Con vs code aprire la cartella htdocs

-su terminale: git clone https://github.com/xxdema10xx/ElectronicRegister

-chiudere e riaprire vs code, aprendo la cartella ElectronicRegister che si è creata in htdocs





