# ETHMomentumFVG — Contexte Strategie NinjaTrader — MAJ 2026-04-22

## Objectif
Portage de BTCMomentumFVG sur Ethereum (MET - Micro Ether).
Meme logique Momentum FVG always-in, adaptee aux caracteristiques de l'ETH.
Diversification crypto pour reduire le risque de concentration BTC.

## Utilisateur
- Trader FR, C#/NinjaScript
- Setup dev : Mac + Parallels Windows (NinjaTrader cote Windows)

## Strategie — V1 baseline

### ETHMomentumFVG.cs
- Trend-following always-in base sur Fair Value Gaps, 4h ETHUSD
- Portage direct de BTCMomentumFVG V7.1 avec seul MinFVG adapte
- Instrument : **MET** (Micro Ether 0.1 ETH, ~250$ notionnel)

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
- Contient : compteurs, Long/Short stats, DD, PnL mensuel/hebdo, ETH weekly prices

## Workflow technique
- Code edite sur Mac : `/Users/lead_scorpion/Desktop/Claude/ETHmomentumFVG/`
- Copie vers `/Users/lead_scorpion/Documents/NinjaTrader 8/bin/Custom/Strategies/`
- NinjaTrader recompile auto (F5 pour forcer)
- Git repo : https://github.com/leadscorpion777/ETHMomentumFVG

## Parametres NinjaTrader importants
- Calcul a la fermeture de la barre (OnBarClose)
- BarsMax = Infini
- Comportement depart : "Attendre d'etre a plat"
- Sortie sur fermeture de session : DECOCHE
- Jours a charger : minimum 90 pour EMA 160

## A faire / questions ouvertes
- [ ] Premier backtest ETH pour valider MinFVGSize 1.0
- [ ] Comparer perf vs BTCMomentumFVG meme periode
- [ ] Ajuster MinFVGSize si besoin apres backtest
- [ ] Si OK, lancer sim temps reel sur DEMO7246610
- [ ] Commit V1 baseline apres premier backtest concluant
