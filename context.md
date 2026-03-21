# Contesto Business — Database MM_PR_MECMATICA_AN

## Sistema

**Mecmatica** è un ERP italiano per la gestione della produzione manifatturiera (make-to-order / make-to-stock). Il database esposto tramite MetronomoMCP è quello di produzione aziendale su **SQL Server 2025**.

Il database contiene centinaia di tabelle raggruppabili in moduli funzionali distinti.

---

## Convenzioni di Naming

- Le chiavi primarie sono sempre `ID<NomeTabella>` (es. `IDArticolo`, `IDCommessa`, `IDContatto`)
- I campi booleani iniziano con `b` (es. `bCliente`, `bFornitore`) oppure sono di tipo `bit`
- I campi di testo descrittivo sono spesso `nvarchar` (supporto Unicode/multilingua)
- I codici umani (codice articolo, codice contatto) sono campi `char` fissi
- Molte tabelle hanno una versione `_DETTAGLIO` (righe) collegata alla testata principale
- Le tabelle `*_EXTRA` contengono campi aggiuntivi opzionali
- Le tabelle `*_OLD` e `*_HISTORY` contengono dati storici/archiviati

---

## Moduli Principali

### 1. Anagrafica Prodotti — `ARTICOLI`

Tabella centrale per tutti i prodotti/semilavorati/materie prime.

**Tabella principale:** `ARTICOLI`

| Campo | Significato |
|---|---|
| `IDArticolo` | PK intera |
| `Articolo` | Codice articolo (char, univoco) |
| `Descrizione` | Descrizione articolo |
| `IDCliente` | Cliente proprietario (se conto-lavoro) |
| `Tipo` | Tipo articolo |
| `Categoria` | Categoria merceologica |
| `UM` | Unità di misura |
| `LPezzo` | Lunghezza pezzo (produzione da barra) |
| `PesoPezzo` | Peso unitario |
| `IDMateriale` | Materiale (per articoli da barra/foglio) |
| `PurchaseType` | 0=produzione interna, 1=acquisto, 2=misto |
| `PrezzoAcquistoMedio` | Prezzo medio di acquisto |

**Tabelle correlate:**
- `ARTICOLI_FASI` — ciclo di lavorazione dell'articolo (fasi produttive)
- `ARTICOLI_COMPOSIZIONE` — distinta base (componenti)
- `ARTICOLI_FORNITORI` — fornitori qualificati per l'articolo
- `ARTICOLI_LISTINO` — prezzi di listino
- `ARTICOLI_DEPOSITI` — giacenze per deposito
- `ARTICOLI_MATRICOLE` — numero di serie/matricola
- `ARTICOLI_REVISIONI` — revisioni del ciclo/distinta
- `ARTICOLI_DESCRIZIONI` — descrizioni multilingua
- `ARTICOLI_ATTRIBUTI_VALORI` — attributi personalizzati
- `ARTICOLI_CLIENTI` — codici articolo lato cliente

---

### 2. Commesse di Produzione — `COMMESSE`

Le commesse sono gli ordini di produzione interni. Ogni commessa produce un articolo specifico.

**Tabella principale:** `COMMESSE`

| Campo | Significato |
|---|---|
| `IDCommessa` | PK intera |
| `Commessa` | Codice commessa (char) |
| `IDArticolo` | Articolo da produrre |
| `IDClienteCommessa` | Cliente di riferimento |
| `Data` | Data creazione |
| `DataConsegna` | Data consegna prevista |
| `DataChiusura` | Data chiusura effettiva |
| `PezziTotale` | Quantità totale da produrre |
| `PezziProdotti` | Quantità già prodotta |
| `PezziVersati` | Quantità versata a magazzino |
| `PezziScartati` | Quantità scartata |
| `Completata` | Flag commessa completata |
| `CommessaStatus` | Stato della commessa |
| `Urgente` | Flag urgenza |
| `IDOrdine` | Ordine cliente collegato |

**Tabelle correlate:**
- `COMMESSE_COMPONENTI` — materiali impegnati nella commessa
- `COMMESSE_FABBISOGNO` — fabbisogno materiali (MRP)
- `COMMESSE_ORE` — ore di lavoro registrate
- `COMMESSE_COSTI` — costi effettivi commessa
- `COMMESSE_IMPEGNI` — impegni di magazzino
- `COMMESSE_MATRICOLE` — matricole prodotte
- `COMMESSE_STATUS` — storico stati commessa
- `COMMESSE_TRACKING` — tracciabilità avanzamento

---

### 3. Contatti (Clienti/Fornitori) — `CONTATTI`

Unica anagrafica per clienti, fornitori e vettori. Il ruolo è determinato da flag booleani.

**Tabella principale:** `CONTATTI`

| Campo | Significato |
|---|---|
| `IDContatto` | PK intera |
| `Codice` | Codice contatto (char) |
| `Descrizione` | Ragione sociale |
| `bCliente` | È un cliente |
| `bFornitore` | È un fornitore |
| `bVettore` | È un vettore/spedizioniere |
| `PartitaIVA` | P.IVA |
| `CF` | Codice fiscale |
| `CodiceSdi` | Codice SDI fatturazione elettronica |
| `Divisa` | Valuta di default |
| `IDPagamentoCredito` | Condizioni di pagamento |
| `ClientePotenziale` | Flag cliente prospect |
| `FornitoreQualificato` | Livello qualifica fornitore |

**Tabelle correlate:**
- `CONTATTI_INDIRIZZI` — indirizzi multipli (spedizione, fatturazione, ecc.)
- `CONTATTI_CATEGORIE` — categorizzazione contatti
- `CONTATTI_DOCUMENTI` — documenti allegati al contatto
- `CONTATTI_ATTRIBUTI_VALORI` — attributi personalizzati

---

### 4. Ordini Clienti — `ORDINI_CLIENTI` / `ORDINI`

Gestione degli ordini ricevuti dai clienti.

**Tabella testata:** `ORDINI_CLIENTI` — l'ordine cliente (testata)
**Tabella righe:** `ORDINI` — le righe dell'ordine (un articolo per riga)

| Campo (ORDINI) | Significato |
|---|---|
| `IDOrdine` | Codice riga ordine |
| `IDOrdineCliente` | Ordine cliente di appartenenza |
| `IDArticolo` | Articolo ordinato |
| `IDCliente` | Cliente |
| `DataConsegna` | Data consegna richiesta |
| `PezziTotale` | Quantità ordinata |
| `PezziConsegnati` | Quantità già consegnata |
| `PezziProdotti` | Quantità prodotta |
| `OrdineStatus` | Stato riga (aperta, chiusa, ecc.) |
| `IDCommessa` (via join) | Commessa di produzione associata |

---

### 5. Fatturazione — `FATTURE` / `FATTURE_FORNITORI`

- `FATTURE` — fatture attive (vendita ai clienti)
- `FATTURE_FORNITORI` — fatture passive (acquisto da fornitori)
- `FATTURE_DETTAGLIO` / `FATTURE_FORNITORI_DETTAGLIO` — righe delle fatture
- `FATTURE_ELETTRONICHE` — fatture XML per SDI (B2B/PA)

| Campo (FATTURE) | Significato |
|---|---|
| `IDFattura` | Codice fattura |
| `IDCliente` | Cliente fatturato |
| `Data` | Data fattura |
| `Imponibile` | Imponibile totale |
| `Iva` | IVA totale |
| `Totale` | Totale fattura |
| `IsVendita` | True=fattura di vendita |
| `IDPagamento` | Condizioni di pagamento |

---

### 6. DDT (Documenti di Trasporto) — `DDT`

Bolle di consegna/trasporto ai clienti.

- `DDT` — testata DDT
- `DDT_DETTAGLIO` — righe DDT
- `DDT_CARICO` / `DDT_CARICO_DETTAGLIO` — DDT di carico (ricezione merci)

---

### 7. Cicli di Lavorazione — `FASI`

Le fasi sono le operazioni produttive (tornio, fresatura, controllo, ecc.).

**Tabella principale:** `FASI`

| Campo | Significato |
|---|---|
| `ID` | PK |
| `DescFase` | Codice fase |
| `Descrizione` | Descrizione operazione |
| `OutSource` | Lavorazione esterna (conto terzi) |
| `Macchina` | Macchina di default |
| `TempoFase` | Tempo ciclo standard |
| `TempoSetUp` | Tempo attrezzaggio |
| `FaseCostoOrario` | Costo orario macchina |
| `FaseCostoOrarioUomo` | Costo orario operatore |

---

### 8. Macchine / Reparti — `MACCHINE`

Parco macchine produttivo con monitoraggio real-time.

| Campo | Significato |
|---|---|
| `IDMacchina` | PK |
| `Macchina` | Codice macchina |
| `Descrizione` | Nome macchina |
| `Reparto` | Reparto di appartenenza |
| `Fermo` | Stato fermo (tipo fermo attuale) |
| `AperturaTurno` | Inizio turno corrente |
| `PezziTotale` | Pezzi prodotti totali |

**Tabelle correlate:**
- `MACCHINE_REPARTI` — raggruppamento per reparto
- `MACCHINE_ORARIO` — orari/turni macchina
- `MACCHINE_CONTATORI` — contatori di produzione
- `MACCHINE_LOG_ALLARMI` — log allarmi macchina
- `MACCHINE_MODELLI` — modelli macchina (template)
- `FERMI_MACCHINA` — storico fermi macchina

---

### 9. ODL (Ordini Di Lavoro) — `CRP_ODL`

Gli ODL sono le sessioni di lavoro su macchina per una commessa.

| Campo | Significato |
|---|---|
| `IDMacchina` | Macchina assegnata |
| `IDCommessa` | Commessa di riferimento |
| `DataApertura` | Inizio lavorazione |
| `DataChiusura` | Fine lavorazione |
| `PezziProdotti` | Pezzi realizzati |
| `PezziScartati` | Pezzi scartati |

---

### 10. Magazzino — `MAG_ARTICOLI_DEPOSITI` / `MAGAZZINO_IMPEGNI`

- `DEPOSITI` — anagrafica depositi/magazzini
- `MAG_ARTICOLI_DEPOSITI` — giacenze per articolo e deposito
- `MAGAZZINO_IMPEGNI` — impegni di magazzino (materiale riservato)
- `ARTICOLI_DEPOSITI` — disponibilità articolo per deposito

---

### 11. Offerte/Preventivi — `OFFERTE`

Gestione commerciale offerte ai clienti.

- `OFFERTE` — testata offerta (con revisioni)
- `OFFERTE_DETTAGLIO` — righe offerta

---

### 12. Manutenzione — `MANU_*`

Modulo CMMS (manutenzione macchine e impianti).

- `MANU_IMPIANTI` — impianti soggetti a manutenzione
- `MANU_INTERVENTI` — interventi manutentivi
- `MANU_MODELLI` — modelli di manutenzione programmata
- `MANU_INCIDENT` — segnalazione guasti/incidenti
- `MANU_CANONI` — contratti di manutenzione

---

### 13. Energia — `ENERGY_*`

Monitoraggio consumi energetici per macchina e ODL.

- `ENERGY_SLOT` — slot di misurazione energetica
- `ENERGY_SLOT_ODL` — energia consumata per ODL
- `ENERGY_SLOT_MACCHINE` — energia per macchina
- `ENERGY_PARAMETRI` — parametri di configurazione

---

### 14. CRM — `CRM_*`

- `CRM_APPUNTAMENTI` — appuntamenti commerciali
- `CRM_ATTIVITA` — attività CRM
- `CRM_AGENTI` — agenti di vendita e provvigioni
- `CRM_OPTIN` — gestione consensi marketing

---

### 15. Contabilità — `CONTAB_*`

- `CONTAB_PC_SOTTOCONTI` — piano dei conti
- `CONTAB_CENTRI_COSTO` — centri di costo
- `CONTAB_CASHFLOW` — previsioni cash flow
- `CONTAB_BUDGET` — budget aziendale
- `CONTAB_DIVISE` — tassi di cambio valute

---

### 16. Formazione — `FORM_*`

Gestione competenze e formazione del personale.

- `FORM_OPERATORI` — operatori/dipendenti
- `FORM_MANSIONI` — mansioni e competenze richieste
- `FORM_CORSI` — corsi di formazione
- `FORM_DPI` — dispositivi di protezione individuale

---

### 17. Business Intelligence — `BINT_*`

- `BINT_CUBE` — cubi dati per analisi
- `BINT_CUSTOM_TABLES` — tabelle custom per reportistica

---

## Relazioni Chiave

```
CONTATTI (clienti/fornitori)
    ↕
ORDINI_CLIENTI → ORDINI → COMMESSE → CRP_ODL → MACCHINE
                              ↕              ↕
                         ARTICOLI        FASI
                              ↕
                    ARTICOLI_COMPOSIZIONE (distinta base)
                    ARTICOLI_FASI (ciclo di lavorazione)

COMMESSE → DDT → FATTURE
        → MAGAZZINO_IMPEGNI
        → COMMESSE_COSTI
```

---

## Query di Esempio Frequenti

### Commesse aperte per cliente
```sql
SELECT c.Commessa, a.Articolo, a.Descrizione,
       c.PezziTotale, c.PezziProdotti, c.DataConsegna,
       co.Descrizione AS Cliente
FROM COMMESSE c
JOIN ARTICOLI a ON c.IDArticolo = a.IDArticolo
JOIN CONTATTI co ON c.IDClienteCommessa = co.IDContatto
WHERE c.Completata = 0
ORDER BY c.DataConsegna
```

### Giacenze a magazzino
```sql
SELECT a.Articolo, a.Descrizione, d.Deposito,
       mad.Giacenza, mad.Impegnato, mad.Disponibile
FROM MAG_ARTICOLI_DEPOSITI mad
JOIN ARTICOLI a ON mad.IDArticolo = a.IDArticolo
JOIN DEPOSITI d ON mad.IDDeposito = d.IDDeposito
WHERE mad.Giacenza > 0
```

### Fatturato per cliente nell'anno corrente
```sql
SELECT co.Descrizione AS Cliente,
       SUM(f.Imponibile) AS Imponibile,
       SUM(f.Totale) AS Totale
FROM FATTURE f
JOIN CONTATTI co ON f.IDCliente = co.IDContatto
WHERE YEAR(f.Data) = YEAR(GETDATE())
  AND f.IsVendita = 1
GROUP BY co.IDContatto, co.Descrizione
ORDER BY SUM(f.Totale) DESC
```

### Produzione giornaliera per macchina
```sql
SELECT m.Macchina, m.Descrizione,
       SUM(o.PezziProdotti) AS PezziOggi
FROM CRP_ODL o
JOIN MACCHINE m ON o.IDMacchina = m.IDMacchina
WHERE CAST(o.DataApertura AS DATE) = CAST(GETDATE() AS DATE)
GROUP BY m.IDMacchina, m.Macchina, m.Descrizione
ORDER BY PezziOggi DESC
```

---

## Note Operative

- **Solo lettura**: MetronomoMCP esegue esclusivamente query SELECT. Nessuna modifica ai dati è possibile.
- **Dati sensibili**: il database contiene dati commerciali reali (clienti, prezzi, fatture). Usare con riservatezza.
- **Performance**: tabelle come `CRP_ODL`, `DOCUMENTI_LOG`, `MAFI_MOVIMENTI` possono contenere milioni di righe. Filtrare sempre per data o chiave.
- **MaxRowsReturned**: il server restituisce al massimo 1000 righe per query. Usare `TOP` o filtri per query analitiche.
