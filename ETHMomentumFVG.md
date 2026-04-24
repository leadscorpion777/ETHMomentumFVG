# ETHMomentumFVG — Contexte Strategie NinjaTrader — MAJ 2026-04-24

## Objectif
Portage de BTCMomentumFVG sur Ethereum (MET - Micro Ether).
Meme logique Momentum FVG always-in, adaptee aux caracteristiques de l'ETH.
Diversification crypto pour reduire le risque de concentration BTC.

## Utilisateur
- Trader FR resident au **Cambodge** (Siem Reap)
- C#/NinjaScript
- Setup dev : Mac + Parallels Windows (NinjaTrader cote Windows)
- Capital trading : **10 000$**

## Strategie — V1 baseline

### ETHMomentumFVG.cs
- Trend-following always-in base sur Fair Value Gaps, 4h
- Portage direct de BTCMomentumFVG V7.1 avec seul MinFVG adapte
- Instrument cible reel : **MET** (Micro Ether 0.1 ETH, ~250$ notionnel)

### Parametres par defaut
- Timeframe : 4h
- Qty : 1 contrat (DefaultQuantity NT)
- ADX Minimum : 20, Periode 14
- EMA Filter : OFF (EMAPeriod 160 dispo)
- Anti-whipsaw : MinTradeDurationH = 12h
- Mercredi : desactive (meme filtre que BTC)
- TPs : OFF (trailing naturel FVG)
- **MinFVGSize : 1.0$** (vs BTC 60$, Gold 3$ — adapte au prix ETH ~3000$)
- Opacite FVG : 25

### Auto-export stats
- Fichier : `C:\Mac\Home\Documents\NinjaTrader 8\last_backtest_ETHMomentumFVG.log`
- Ecrit via `NinjaTrader.Core.Globals.UserDataDir` (compatibilite Parallels)
- Update automatique a chaque backtest (State.Terminated)

## ⚠️ CONSTATS CRITIQUES (session 23-24 avril 2026)

### Les backtests initiaux etaient biaises
Les resultats apparemment profitables (+13 720$ ETHUSD, +195 884$ BTCUSD) ont ete
obtenus dans des conditions **non representatives** de la realite tradable :

1. **Instrument = CFD FXCM**, pas CME
   - BTCUSD et ETHUSD dans NT = CFD FXCM (Type d'instrument : CFD, URL fxcm.com)
   - Non tradable en France (AMF interdit CFD crypto aux residents UE depuis 2020)
   - Donnees differentes du vrai marche spot et des futures CME
   - Tous les dev d'1 mois etaient bases sur ces donnees non tradables

2. **"Arret en fin de journee" cochee** = artefact majeur
   - Force le backtest a flatter toutes positions a chaque fin de session
   - Ajoute un "stop temporel quotidien" artificiel
   - L'edge positif venait de ce stop, PAS de la logique FVG
   - Sans cette option : strat **negative**
   - En live, cette option n'existe pas → live = negatif

3. **Session template incorrecte** ("CME US Index Futures ETH" sur instrument 24/7)
   - Cree des "fins de session" artificielles a 16h CT
   - Combinaison avec option 2 produisait un bias positif total

### Resultats reels sur instrument tradable (MET CME, Default 24x5)
- **PF 1,12** (quasi-random)
- **PnL +754$ sur ~5 ans** (MET lance fin 2021)
- **Max DD 137$** ramene MET
- **Time Underwater 527 jours** (17,6 mois !)
- Commissions MET ~1,30$/RT × 3 624 trades = 4 711$ frais > 754$ PnL
- **Strategie nette perdante en live** sur MET CME

### Confirmations cross-actifs
- **MBT CME** : PnL -5 000$ sur 5 ans (-330 000$ sur BTC full, contrat 5 BTC)
- **GC (Gold) CME** : PnL +15 023$ mais DD -137 369$, ratio 0,11 vs B&H 3,22
- **B&H 1 GC** : +259 020$ (la strat sous-performe massivement le buy & hold)

### Verdict
**Momentum FVG always-in 4h ne marche ni sur crypto CME ni sur Gold CME.**
Les resultats initiaux etaient des artefacts de backtest. Pas d'edge reel.

## Session templates a utiliser (important)

| Instrument | Aujourd'hui (avant 29/05/2026) | Apres 29/05/2026 |
|---|---|---|
| BTCUSD/ETHUSD (CFD FXCM) | Cryptomonnaie | Cryptomonnaie |
| MBT/MET/MSL/MXRP (CME) | **Default 24 x 5** | Cryptomonnaie |
| GC/MGC (Gold CME) | CME Commodities ETH | CME Commodities ETH |
| ES/MES/NQ/MNQ | CME US Index Futures ETH | CME US Index Futures ETH |

**"Arret en fin de journee" = toujours DECOCHE** pour strats always-in multi-jour.

## Nouveautes CME 2026

### Crypto futures lancees ou annoncees
- **ADA (Cardano)** — 9 fev 2026 ✅
- **LINK (Chainlink)** — 9 fev 2026 ✅
- **XLM (Stellar)** — 9 fev 2026 ✅
- **AVAX (Avalanche)** — 4 mai 2026 (prevu)
- **SUI** — 4 mai 2026 (prevu)

### Changement majeur : 24/7 sur CME crypto
- **Demarre 29 mai 2026**
- Plus de gaps weekends sur MBT/MET/MSL/etc
- Plus de fermetures de sessions
- Marge probablement unifiee (plus d'intraday vs overnight)
- Resout le probleme structurel des backtests spot vs futures

## Infrastructure

### VPS
- **Cible : Contabo Cloud VPS 20 — region SINGAPORE (SIN)**
- Latence RDP depuis Cambodge : ~45ms (fluide)
- Latence CME depuis SG : ~200ms (negligeable pour 4h, OK pour 5min)
- Prix : ~27€/mois

### Latence et choix actifs
- **4h / 1h** : Singapour = zero probleme, latence invisible
- **5-15min algo** : Singapour suffit, latence ~0,07% d'une bougie
- **1min algo** : Singapour OK, commissions plus critiques que latence
- **Sub-minute tick scalping** : PAS VIABLE retail depuis SG ni ailleurs (HFT territory)

## Contraintes reglementaires FR

### Crypto tradable pour un resident FR retail
- **CME futures via NT+NTB** : BTC, ETH, SOL, XRP, ADA, LINK, XLM + AVAX/SUI bientot ✅
- **Binance EU spot** : long only, pas de levier, futures interdits ❌ pour strat always-in
- **Bybit/OKX/Binance Int'l** : zone grise reglementaire FR ❌
- **BlackBull CFD cTrader** : NZ non-AMF, CFD = pas tradable propre ❌
- **Coinbase Derivatives** : reserve US retail, pas accessible FR ❌

**Seul canal propre = CME futures via NT.**

## Capital 10k et portfolio realiste

### Marges overnight CME crypto (tous ~500-1100$ par contrat)
- MBT : ~1 100$ (11% cap)
- MET : ~400-500$ (5% cap)
- MSL (Solana) : ~800-1 000$ (9%)
- MXRP : ~600-800$ (7%)

### Portfolio max viable avec 10k
- 3-4 positions simultanees max
- Total marge 2 000-3 500$
- DD buffer 15-20% = 1 500-2 000$
- 30-50% du cap mobilise
- 50% cash libre

## Pivot strategique (post-session 24 avril 2026)

### Abandon ETHMomentumFVG V1 en l'etat
La strat always-in Momentum FVG **n'a pas d'edge** sur les futures CME tradables.
Continuer a la developper = perte de temps.

### Nouvelle direction : Portfolio multi-strats classiques
Bascule vers **strategies documentees et robustes** sur les meilleurs micros CME :

**Les 4 meilleurs micros CME retail (criteres : liquidite, decorrelation, data riche) :**
1. **MES** (Micro S&P 500) - le future le plus liquide au monde
2. **MNQ** (Micro Nasdaq) - volatil, momentum tech
3. **MGC** (Micro Gold) - decorrele crypto, trending
4. **M6E** (Micro EUR/USD) - forex le plus propre

### Recette minimum viable par style

**Trend-Following** (TF optimal 1h-4h)
- Donchian 20/55 breakout
- MA Crossover 50/200
- Filtre ADX > 25

**Mean-Reversion** (TF optimal 5-15min)
- Bollinger Bands + RSI confluence
- Z-Score extremes
- CCI (interessant, moins sature)
- Regression Channel (combine trend + MR)
- Filtre ADX < 20

**Breakout** (TF optimal 15min-1h, ou ORB)
- ORB 30min sur MES - le "Hello World" retail
- Donchian 20 sur MGC
- Volume confirmation obligatoire

### Premiere strat "Hello World" recommandee
**ORB 30min sur MES 5min** :
- Range 9h30-10h NY
- Entry breakout avec volume filter (> MA(vol,20) × 1,3)
- Stop opposite side of range
- Target 2× range ou trailing ATR 3×
- Flat avant 15h45 NY
- Session US = 21h30-05h Cambodge (compatible fuseau)

## Workflow technique (inchange)
- Code edite sur Mac : `/Users/lead_scorpion/Desktop/Claude/ETHmomentumFVG/`
- Copie vers `/Users/lead_scorpion/Documents/NinjaTrader 8/bin/Custom/Strategies/`
- NinjaTrader recompile auto (F5 pour forcer)
- Git repo : https://github.com/leadscorpion777/ETHMomentumFVG

## Parametres NinjaTrader importants
- Calcul a la fermeture de la barre (OnBarClose)
- BarsMax = Infini
- Comportement depart : "Attendre d'etre a plat"
- Sortie sur fermeture de session : DECOCHE
- Arret en fin de journee : **DECOCHE** (pour strats always-in)
- Jours a charger : minimum 90 pour EMA 160

## A faire / questions ouvertes

### Abandon des pistes
- ❌ Developper plus loin ETHMomentumFVG V1 (pas d'edge reel)
- ❌ Explorer Binance EU / cTrader / CFD (pas viable)
- ❌ VPS Chicago / scalping sub-minute (pas retail)

### Pistes actives
- [ ] Refaire backtest ETHMomentumFVG avec **bons params** (Default 24x5, Arret fin journee DECOCHE) sur MET pour confirmer PF reel
- [ ] **Coder ORB 30min sur MES** comme premiere strat "Hello World" propre
- [ ] Si ORB validee : porter sur MNQ et MGC
- [ ] Tester Mean-Reversion 5-15min (Z-Score + ADX filter) sur MES
- [ ] Tester Regression Channel MR sur MGC 4h
- [ ] Apres 29 mai 2026 : relancer tous les backtests crypto CME en 24/7 (gaps disparaissent)
- [ ] Preparer portfolio 3-4 strats decorrelees (TF + MR + Breakout) pour lisser variance mensuelle

## Objectif revenu mensuel

### Pour viser 500-1000$/mois avec 10k capital
- Sharpe cible > 1,5 en OOS
- Max DD < 10% capital
- 80%+ mois positifs
- Worst month < 3% capital

### Ordre de developpement recommande
1. **1 strat validee en sim 3 mois** (ORB MES)
2. **+ 1 strat decorrelee** (MR 5-15min)
3. **+ 1 strat breakout swing** (Donchian MGC)
4. **Multi-timezone** apres capital 20k+ (Asian futures via IBKR)

## Lecons retenues de la session 23-24 avril 2026

1. **Toujours verifier le type d'instrument** (Future vs CFD) avant tout dev
2. **"Arret en fin de journee" DECOCHE** sur strats multi-jour
3. **Session template coherente** avec l'instrument
4. **Backtest sur instrument tradable directement** (pas sur CFD "proche")
5. **PF > 1,5 requis** sur OOS avant de parler d'edge reel
6. **Les 195k$ et 13k$ initiaux etaient des mirages de backtest**
7. **Strat simple qui marche > strat complexe qui mirage**
