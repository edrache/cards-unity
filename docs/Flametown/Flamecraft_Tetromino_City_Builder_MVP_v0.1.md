# Flamecraft Tetromino City Builder -- MVP Design Document (v0.1)

## 1. High Concept

Gra typu cozy city builder osadzona w świecie **Flamecraft**. Gracz
rozbudowuje miasto, dokładając wielopolowe klocki (na początku wyłącznie
tetromino), z których każdy reprezentuje fragment miasta złożony ze
sklepów i innych elementów świata.

Inspiracje: - Flamecraft - Dorfromantik - Tetris - Vampire Survivors
(tempo rozwoju -- poza MVP)

------------------------------------------------------------------------

## 2. Filary projektu

-   Budowanie miasta z wielopolowych klocków.
-   Każda decyzja jest szybka i czytelna.
-   Miasto stopniowo rozrasta się w spójną całość.
-   Świat i klimat Flamecraft są integralną częścią gry.
-   Gra stawia na relaksującą rozgrywkę.

------------------------------------------------------------------------

## 3. Design Rules

1.  Jedno pole klocka = jeden element miasta.
2.  W MVP wszystkie klocki są tetromino (4 pola).
3.  System powinien umożliwiać w przyszłości klocki dowolnych rozmiarów.
4.  Drogi znajdują się wyłącznie na krawędziach pól.
5.  Drogi mogą występować losowo na każdej krawędzi.
6.  W przyszłości wszystkie postacie poruszają się wyłącznie po drogach.
7.  Mechaniki powinny wynikać z istniejących systemów.
8.  Prostota jest ważniejsza od liczby mechanik.
9.  Projekt pozostaje wierny światu Flamecraft.

------------------------------------------------------------------------

## 4. Core Loop (MVP)

1.  Otrzymaj kolejny klocek.
2.  Obróć go.
3.  Wybierz miejsce.
4.  Dołóż do miasta.
5.  Powtórz.

------------------------------------------------------------------------

## 5. Tetromino

-   Każdy klocek składa się z 4 pól.
-   Każde pole zawiera dokładnie jeden element miasta.
-   Klocki można obracać.
-   Miasto rozbudowuje się poprzez dokładanie kolejnych klocków.

------------------------------------------------------------------------

## 6. Elementy miasta (MVP)

Wyłącznie wizualne.

Przykłady: - sklepy Flamecraft, - domy, - place, - parki, - fontanny, -
dekoracje.

------------------------------------------------------------------------

## 7. Drogi

-   Rysowane wyłącznie na krawędziach pól.
-   Mogą występować na dowolnej krawędzi.
-   W MVP pełnią wyłącznie funkcję wizualną.
-   Docelowo staną się siecią, po której poruszają się wszystkie
    postacie.

------------------------------------------------------------------------

## 8. Flamecraft

Docelowo gra wykorzystuje: - sklepy, - rzemieślników (Artisan
Dragons), - Fancy Dragons, - towary, - monety, - nazewnictwo i klimat
świata.

W MVP elementy te są jedynie wizualne.

------------------------------------------------------------------------

## 9. MVP Scope

### Zawiera

-   dokładanie klocków,
-   obracanie,
-   losowanie kolejnego klocka,
-   wizualne elementy miasta,
-   wizualne drogi,
-   prosty interfejs,
-   kamerę.

### Nie zawiera

-   rozwoju,
-   zdolności,
-   ekonomii,
-   punktów,
-   celu gry,
-   zakończenia,
-   ruchu postaci,
-   działania sklepów,
-   mechanik smoków.

------------------------------------------------------------------------

## 10. Backlog

### Rozwój

-   nowe typy klocków,
-   nowe sklepy,
-   zdolności,
-   rozwój inspirowany Vampire Survivors.

### Drogi

-   ruch smoków,
-   klienci,
-   dostawy,
-   synergie.

### Sklepy

-   efekty,
-   produkcja,
-   aktywacje.

### Smoki

-   Artisan Dragons,
-   Fancy Dragons,
-   specjalne zdolności.

------------------------------------------------------------------------

## 11. Otwarte pytania

-   Jak gracz otrzymuje kolejne klocki?
-   Czy będzie talia klocków?
-   Czy można wybierać spośród kilku klocków?
-   Czy istnieją zasoby?
-   Jak wygląda rozwój po MVP?
-   Czy pojawi się ekonomia?
-   Co jest długoterminowym celem gracza?
-   Jaki jest warunek zakończenia gry?
