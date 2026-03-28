# Contesto Pianificazione Produzione

## Obiettivo

Questo contesto integra il contesto business generale con regole e logiche specifiche per la **pianificazione della produzione** nel sistema Metronomo.Net.

---

## Flusso di Pianificazione

### Dalla Riga Ordine alla Commessa

Il ciclo parte sempre da una riga di ordine cliente (`ORDINI`):
1. La riga ordine indica articolo, quantità e data consegna richiesta
2. Viene creata una `COMMESSA` collegata alla riga ordine tramite `IDOrdine`
3. La commessa viene agganciata alla riga tramite `COMMESSE_IMPEGNI`
4. Se l'articolo ha una distinta base multilivello, vengono create **sottocommesse** per i componenti e collegate tramite `COMMESSE_COMPONENTI`

### Esplosione della Distinta Base

- Il pianificatore esplode la distinta base (`ARTICOLI_COMPOSIZIONE`) per calcolare i componenti necessari
- I componenti di acquisto generano richieste di acquisto
- I componenti di produzione interna generano sottocommesse
- Il fabbisogno materiali è tracciato in `COMMESSE_FABBISOGNO`

### Generazione degli ODL

Per ogni commessa, il ciclo di lavorazione (`ARTICOLI_FASI`) viene utilizzato per generare gli ODL (`CRP_ODL`):
- Ogni fase dell'articolo diventa un ODL su una specifica macchina
- Gli ODL seguono l'ordine delle fasi definito in `ARTICOLI_FASI`
- Il campo `IDFase` collega l'ODL alla specifica fase produttiva

---

## Vincoli di Pianificazione

- La data di consegna della commessa deve rispettare quella dell'ordine cliente
- Le sottocommesse devono essere completate prima della commessa padre
- Le fasi produttive di una commessa devono rispettare la sequenza definita in `ARTICOLI_FASI`
- La capacità delle macchine è il principale collo di bottiglia

---

## Query Utili per la Pianificazione

### Carico macchine per il periodo
```sql
SELECT m.Macchina, m.Descrizione,
       COUNT(o.IDOdl) AS ODLAperti,
       SUM(o.PezziTotale - o.PezziProdotti) AS PezziResidui
FROM CRP_ODL o
JOIN MACCHINE m ON o.IDMacchina = m.IDMacchina
WHERE o.DataChiusura IS NULL
  AND o.DataApertura <= DATEADD(day, 30, GETDATE())
GROUP BY m.IDMacchina, m.Macchina, m.Descrizione
ORDER BY ODLAperti DESC
```

### Commesse in ritardo
```sql
SELECT c.Commessa, a.Articolo, a.Descrizione,
       c.DataConsegna, c.PezziTotale, c.PezziProdotti,
       co.Descrizione AS Cliente,
       DATEDIFF(day, c.DataConsegna, GETDATE()) AS GiorniRitardo
FROM COMMESSE c
JOIN ARTICOLI a ON c.IDArticolo = a.IDArticolo
JOIN CONTATTI co ON c.IDClienteCommessa = co.IDContatto
WHERE c.Completata = 0
  AND c.DataConsegna < GETDATE()
ORDER BY GiorniRitardo DESC
```

### Fabbisogno materiali non soddisfatto
```sql
SELECT f.IDCommessa, c.Commessa,
       a.Articolo, a.Descrizione,
       f.Quantita, f.QuantitaDisponibile,
       f.Quantita - f.QuantitaDisponibile AS Mancante
FROM COMMESSE_FABBISOGNO f
JOIN COMMESSE c ON f.IDCommessa = c.IDCommessa
JOIN ARTICOLI a ON f.IDArticolo = a.IDArticolo
WHERE f.Quantita > f.QuantitaDisponibile
  AND c.Completata = 0
ORDER BY c.DataConsegna
```

### Gerarchia commessa padre / sottocommesse
```sql
SELECT cp.Commessa AS CommessaPadre,
       ap.Articolo AS ArticoloPadre,
       cf.Commessa AS Sottocommessa,
       ac.Articolo AS Componente,
       cc.Quantita
FROM COMMESSE_COMPONENTI cc
JOIN COMMESSE cp ON cc.IDCommessa = cp.IDCommessa
JOIN COMMESSE cf ON cc.IDCommessaFiglio = cf.IDCommessa
JOIN ARTICOLI ap ON cp.IDArticolo = ap.IDArticolo
JOIN ARTICOLI ac ON cf.IDArticolo = ac.IDArticolo
WHERE cp.Completata = 0
ORDER BY cp.Commessa, cc.Sequenza
```
