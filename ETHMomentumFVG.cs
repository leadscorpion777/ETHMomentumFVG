#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class ETHMomentumFVG : Strategy
	{
		#region Variables

		private double		fvgTop;
		private double		fvgBottom;
		private bool		fvgIsBullish;
		private bool		hasActiveFvg;
		private int			currentDirection;	// 1=long, -1=short, 0=flat (attente)
		private int			totalQuantity;
		private int			fvgCount;

		private double[]	tpLevels;
		private int[]		tpQuantities;
		private bool[]		tpHit;
		private double		entryPrice;
		private bool		beActive;		// BE actif apres TP2

		private DateTime	entryTime;			// Heure d'entree du trade actuel
		private int			statSwitchBlocked;	// Switches bloques par le filtre duree
		private SortedDictionary<string, double> ethWeeklyPrice;	// Prix ETH dernier close de la semaine
		// Stats (compteurs internes - PnL via SystemPerformance)
		private int			statSwitchCount;
		private int[]		statTPHits;
		private int			statInvalidations;
		private int			statBEHits;

		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "Momentum FVG - Toujours en position (Long ou Short). Trailing naturel par FVG. TPs optionnels sur pivots.";
				Name						= "ETHMomentumFVG";
				Calculate					= Calculate.OnBarClose;
				EntriesPerDirection			= 1;
				EntryHandling				= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy = false;
				IsFillLimitOnTouch			= false;
				MaximumBarsLookBack			= MaximumBarsLookBack.Infinite;
				StartBehavior				= StartBehavior.WaitUntilFlat;
				IsInstantiatedOnEachOptimizationIteration = true;

				// Position
				// Filtre jours
				TradeLundi					= true;
				TradeMardi					= true;
				TradeMercredi				= false;
				TradeJeudi					= true;
				TradeVendredi				= true;
				TradeSamedi					= true;
				TradeDimanche				= true;

				// Filtre ADX
				ADXMinimum					= 20;
				ADXPeriod					= 14;

				// Filtre EMA (tendance)
				EnableEMAFilter				= false;
				EMAPeriod					= 160;

				// Filtre anti-whipsaw
				MinTradeDurationH			= 12;

				// TPs (desactives par defaut)
				EnableTPs					= false;
				PivotLength					= 5;
				MinTPDistPct				= 0.5;
				DiscoveryPct				= 1.0;
				TP1Pct						= 40;
				TP2Pct						= 30;
				TP3Pct						= 20;
				TP4Pct						= 10;

				// FVG Filter
				MinFVGSize					= 1.0;

				// Visuel
				FvgOpacity					= 25;
			}
			else if (State == State.Configure)
			{
				currentDirection = 0;
				hasActiveFvg	= false;
				fvgCount		= 0;
				tpLevels		= new double[4];
				tpQuantities	= new int[4];
				tpHit			= new bool[4];
				entryPrice		= 0;
				beActive		= false;

				entryTime			= DateTime.MinValue;
				statSwitchBlocked	= 0;
				statSwitchCount		= 0;
				statTPHits			= new int[4];
				statInvalidations	= 0;
				statBEHits			= 0;
				ethWeeklyPrice		= new SortedDictionary<string, double>();
			}
			else if (State == State.Terminated)
			{
				if (SystemPerformance != null && SystemPerformance.AllTrades.Count > 0)
					PrintStats();
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < Math.Max(2 * PivotLength + 1, 3))
				return;

			// Tracker le prix ETH par semaine (dernier close de la semaine)
			var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
			int w = cal.GetWeekOfYear(Time[0], System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
			string wk = Time[0].Year + "-W" + w.ToString("D2");
			ethWeeklyPrice[wk] = Close[0];

			// 0. Filtre jour de la semaine
			if (!IsDayAllowed())
				return;

			// 1. Detection nouvelle FVG
			bool newFvg = DetectFVG();

			// 2. Si pas de nouvelle FVG, verifier invalidation de la FVG active
			if (!newFvg && hasActiveFvg)
				CheckInvalidation();

			// 3. Verifier les Take Profits (si actives)
			if (EnableTPs && currentDirection != 0)
				CheckTakeProfits();
		}

		#region FVG Detection

		private bool DetectFVG()
		{
			if (Low[0] > High[2])
			{
				double top		= Low[0];
				double bottom	= High[2];
				double size		= top - bottom;

				if (size < MinFVGSize)
				{
					Print(Time[0] + " | BULLISH FVG FILTERED | Size=" + size.ToString("F2") + " < Min=" + MinFVGSize.ToString("F2"));
					return false;
				}

				Print(Time[0] + " | NEW BULLISH FVG | Top=" + top.ToString("F2") + " Bottom=" + bottom.ToString("F2") + " | Size=" + size.ToString("F2"));
				DrawFVGBox(top, bottom);
				SetActiveFVG(top, bottom, true);

				if (currentDirection != 1)
				{
					if (ADXMinimum > 0 && ADX(ADXPeriod)[0] < ADXMinimum)
					{
						Print(Time[0] + " | LONG BLOQUE PAR ADX | ADX=" + ADX(ADXPeriod)[0].ToString("F1") + " < Min=" + ADXMinimum);
						return false;
					}
					if (EnableEMAFilter && Close[0] < EMA(Close, EMAPeriod)[0])
					{
						Print(Time[0] + " | LONG BLOQUE PAR EMA | Close=" + Close[0].ToString("F2") + " < EMA" + EMAPeriod + "=" + EMA(Close, EMAPeriod)[0].ToString("F2"));
						return false;
					}
					if (IsSwitchAllowed())
						SwitchPosition(1);
					else
						return false;
				}

				return true;
			}

			if (High[0] < Low[2])
			{
				double top		= Low[2];
				double bottom	= High[0];
				double size		= top - bottom;

				if (size < MinFVGSize)
				{
					Print(Time[0] + " | BEARISH FVG FILTERED | Size=" + size.ToString("F2") + " < Min=" + MinFVGSize.ToString("F2"));
					return false;
				}

				Print(Time[0] + " | NEW BEARISH FVG | Top=" + top.ToString("F2") + " Bottom=" + bottom.ToString("F2") + " | Size=" + size.ToString("F2"));
				DrawFVGBox(top, bottom);
				SetActiveFVG(top, bottom, false);

				if (currentDirection != -1)
				{
					if (ADXMinimum > 0 && ADX(ADXPeriod)[0] < ADXMinimum)
					{
						Print(Time[0] + " | SHORT BLOQUE PAR ADX | ADX=" + ADX(ADXPeriod)[0].ToString("F1") + " < Min=" + ADXMinimum);
						return false;
					}
					if (EnableEMAFilter && Close[0] > EMA(Close, EMAPeriod)[0])
					{
						Print(Time[0] + " | SHORT BLOQUE PAR EMA | Close=" + Close[0].ToString("F2") + " > EMA" + EMAPeriod + "=" + EMA(Close, EMAPeriod)[0].ToString("F2"));
						return false;
					}
					if (IsSwitchAllowed())
						SwitchPosition(-1);
					else
						return false;
				}

				return true;
			}

			return false;
		}

		private void SetActiveFVG(double top, double bottom, bool isBullish)
		{
			fvgTop			= top;
			fvgBottom		= bottom;
			fvgIsBullish	= isBullish;
			hasActiveFvg	= true;
		}

		private void DrawFVGBox(double top, double bottom)
		{
			fvgCount++;
			string tag = "FVG_" + fvgCount;

			Draw.Rectangle(this, tag, true, 2, top, 0, bottom,
				Brushes.Yellow, Brushes.Yellow, FvgOpacity);
		}

		#endregion

		#region Invalidation

		private void CheckInvalidation()
		{
			if (fvgIsBullish && Close[0] < fvgBottom)
			{
				if (!IsSwitchAllowed()) return;
				statInvalidations++;
				Print(Time[0] + " | INVALIDATION Bullish FVG | Close=" + Close[0].ToString("F2") + " < FVG Bottom=" + fvgBottom.ToString("F2"));
				hasActiveFvg = false;
				SwitchPosition(-1);
			}
			else if (!fvgIsBullish && Close[0] > fvgTop)
			{
				if (!IsSwitchAllowed()) return;
				statInvalidations++;
				Print(Time[0] + " | INVALIDATION Bearish FVG | Close=" + Close[0].ToString("F2") + " > FVG Top=" + fvgTop.ToString("F2"));
				hasActiveFvg = false;
				SwitchPosition(1);
			}
		}

		#endregion

		#region Position Management

		private bool IsDayAllowed()
		{
			switch (Time[0].DayOfWeek)
			{
				case DayOfWeek.Monday:		return TradeLundi;
				case DayOfWeek.Tuesday:		return TradeMardi;
				case DayOfWeek.Wednesday:	return TradeMercredi;
				case DayOfWeek.Thursday:	return TradeJeudi;
				case DayOfWeek.Friday:		return TradeVendredi;
				case DayOfWeek.Saturday:	return TradeSamedi;
				case DayOfWeek.Sunday:		return TradeDimanche;
				default: return true;
			}
		}

		private bool IsSwitchAllowed()
		{
			if (MinTradeDurationH <= 0 || currentDirection == 0 || entryTime == DateTime.MinValue)
				return true;

			double hoursInTrade = (Time[0] - entryTime).TotalHours;
			if (hoursInTrade < MinTradeDurationH)
			{
				statSwitchBlocked++;
				Print(Time[0] + " | SWITCH BLOQUE | Duree=" + hoursInTrade.ToString("F1") + "h < Min=" + MinTradeDurationH + "h");
				return false;
			}
			return true;
		}

		private void SwitchPosition(int newDirection)
		{
			statSwitchCount++;

			if (currentDirection == 1 && Position.MarketPosition == MarketPosition.Long)
			{
				Print(Time[0] + " | CLOSE LONG @ " + Close[0].ToString("F2"));
				ExitLong("SwitchExit", "LongEntry");
			}
			else if (currentDirection == -1 && Position.MarketPosition == MarketPosition.Short)
			{
				Print(Time[0] + " | CLOSE SHORT @ " + Close[0].ToString("F2"));
				ExitShort("SwitchExit", "ShortEntry");
			}

			totalQuantity = DefaultQuantity;
			if (totalQuantity < 1)
				totalQuantity = 1;

			string dir = newDirection == 1 ? "LONG" : "SHORT";
			Print(Time[0] + " | OPEN " + dir + " | Qty=" + totalQuantity + " | Price=" + Close[0].ToString("F2"));

			if (newDirection == 1)
				EnterLong(totalQuantity, "LongEntry");
			else if (newDirection == -1)
				EnterShort(totalQuantity, "ShortEntry");

			currentDirection = newDirection;
			entryTime = Time[0];

			if (EnableTPs)
				CalculateTPs();
		}


		#endregion

		#region Take Profits

		private void CalculateTPs()
		{
			double entry = Close[0];
			entryPrice	= entry;
			beActive	= false;

			if (currentDirection == 1)
				FindTPLongs(entry);
			else
				FindTPShorts(entry);

			// Assigner les quantites
			double[] pcts = { TP1Pct, TP2Pct, TP3Pct, TP4Pct };
			int remainingQty = totalQuantity;

			for (int i = 0; i < 4; i++)
			{
				if (pcts[i] <= 0 || tpLevels[i] == 0)
				{
					tpLevels[i]		= 0;
					tpQuantities[i]	= 0;
					tpHit[i]		= true;
				}
				else
				{
					int qty = (int)Math.Floor(totalQuantity * (pcts[i] / 100.0));
					if (qty > remainingQty) qty = remainingQty;
					if (qty < 1 && remainingQty > 0) qty = 1;

					tpQuantities[i]	= qty;
					tpHit[i]		= false;
					remainingQty   -= qty;

					Print(Time[0] + " |   TP" + (i + 1) + " SET | Level=" + tpLevels[i].ToString("F2") + " | Qty=" + qty + "/" + totalQuantity);
				}
			}
		}

		private void FindTPLongs(double entry)
		{
			double above	= entry;
			double minDist	= MinTPDistPct / 100.0;
			int maxScan		= Math.Min(CurrentBar - PivotLength, 500);
			int found		= 0;

			tpLevels[0] = 0; tpLevels[1] = 0; tpLevels[2] = 0; tpLevels[3] = 0;

			Print(Time[0] + " | PIVOT SEARCH | Direction=HIGH | Price=" + entry.ToString("F2") + " | Lookback=" + maxScan + " bars | MinDist=" + MinTPDistPct + "%");

			for (int i = PivotLength + 1; i <= maxScan; i++)
			{
				double h = High[i];
				double minReqNow = above * (1 + minDist);

				if (h < minReqNow) continue;

				bool ok = true;
				for (int j = 1; j <= PivotLength; j++)
				{
					if (High[i - j] >= h || High[i + j] >= h) { ok = false; break; }
				}

				if (ok)
				{
					found++;
					Print(Time[0] + " |   TP" + found + " PIVOT HIGH found | Date=" + Time[i].ToString("dd/MM/yyyy") + " | Level=" + h.ToString("F2") + " | BarsAgo=" + i);

					if      (found == 1) { tpLevels[0] = h; above = h; }
					else if (found == 2) { tpLevels[1] = h; above = h; }
					else if (found == 3) { tpLevels[2] = h; above = h; }
					else if (found == 4) { tpLevels[3] = h; break; }
				}
			}

			// Discovery : si pas assez de pivots, projeter +DiscoveryPct%
			double disc = DiscoveryPct / 100.0;
			if (tpLevels[0] == 0) { tpLevels[0] = RoundPrice(entry * (1 + disc)); Print(Time[0] + " |   TP1 DISCOVERY | Level=" + tpLevels[0].ToString("F2")); }
			if (tpLevels[1] == 0) { tpLevels[1] = RoundPrice(tpLevels[0] * (1 + disc)); Print(Time[0] + " |   TP2 DISCOVERY | Level=" + tpLevels[1].ToString("F2")); }
			if (tpLevels[2] == 0) { tpLevels[2] = RoundPrice(tpLevels[1] * (1 + disc)); Print(Time[0] + " |   TP3 DISCOVERY | Level=" + tpLevels[2].ToString("F2")); }
			if (tpLevels[3] == 0) { tpLevels[3] = RoundPrice(tpLevels[2] * (1 + disc)); Print(Time[0] + " |   TP4 DISCOVERY | Level=" + tpLevels[3].ToString("F2")); }
		}

		private void FindTPShorts(double entry)
		{
			double below	= entry;
			double minDist	= MinTPDistPct / 100.0;
			int maxScan		= Math.Min(CurrentBar - PivotLength, 500);
			int found		= 0;

			tpLevels[0] = 0; tpLevels[1] = 0; tpLevels[2] = 0; tpLevels[3] = 0;

			Print(Time[0] + " | PIVOT SEARCH | Direction=LOW | Price=" + entry.ToString("F2") + " | Lookback=" + maxScan + " bars | MinDist=" + MinTPDistPct + "%");

			for (int i = PivotLength + 1; i <= maxScan; i++)
			{
				double l = Low[i];
				double maxReqNow = below * (1 - minDist);

				if (l > maxReqNow) continue;

				bool ok = true;
				for (int j = 1; j <= PivotLength; j++)
				{
					if (Low[i - j] <= l || Low[i + j] <= l) { ok = false; break; }
				}

				if (ok)
				{
					found++;
					Print(Time[0] + " |   TP" + found + " PIVOT LOW found | Date=" + Time[i].ToString("dd/MM/yyyy") + " | Level=" + l.ToString("F2") + " | BarsAgo=" + i);

					if      (found == 1) { tpLevels[0] = l; below = l; }
					else if (found == 2) { tpLevels[1] = l; below = l; }
					else if (found == 3) { tpLevels[2] = l; below = l; }
					else if (found == 4) { tpLevels[3] = l; break; }
				}
			}

			// Discovery : si pas assez de pivots, projeter -DiscoveryPct%
			double disc = DiscoveryPct / 100.0;
			if (tpLevels[0] == 0) { tpLevels[0] = RoundPrice(entry * (1 - disc)); Print(Time[0] + " |   TP1 DISCOVERY | Level=" + tpLevels[0].ToString("F2")); }
			if (tpLevels[1] == 0) { tpLevels[1] = RoundPrice(tpLevels[0] * (1 - disc)); Print(Time[0] + " |   TP2 DISCOVERY | Level=" + tpLevels[1].ToString("F2")); }
			if (tpLevels[2] == 0) { tpLevels[2] = RoundPrice(tpLevels[1] * (1 - disc)); Print(Time[0] + " |   TP3 DISCOVERY | Level=" + tpLevels[2].ToString("F2")); }
			if (tpLevels[3] == 0) { tpLevels[3] = RoundPrice(tpLevels[2] * (1 - disc)); Print(Time[0] + " |   TP4 DISCOVERY | Level=" + tpLevels[3].ToString("F2")); }
		}

		private double RoundPrice(double price)
		{
			return Instrument.MasterInstrument.RoundToTickSize(price);
		}

		private void CheckTakeProfits()
		{
			// 1. Verifier les TPs
			for (int i = 0; i < 4; i++)
			{
				if (tpHit[i] || tpLevels[i] == 0 || tpQuantities[i] < 1)
					continue;

				string signal = "TP" + (i + 1);

				if (currentDirection == 1 && Close[0] >= tpLevels[i])
				{
					statTPHits[i]++;
					Print(Time[0] + " | " + signal + " HIT (LONG) | Level=" + tpLevels[i].ToString("F2") + " | Qty=" + tpQuantities[i]);
					ExitLong(tpQuantities[i], signal, "LongEntry");
					tpHit[i] = true;

					if (i == 1 && !beActive)
					{
						beActive = true;
						Print(Time[0] + " |   BE ACTIVE | Level=" + entryPrice.ToString("F2"));
					}
					break; // 1 seul TP par barre
				}
				else if (currentDirection == -1 && Close[0] <= tpLevels[i])
				{
					statTPHits[i]++;
					Print(Time[0] + " | " + signal + " HIT (SHORT) | Level=" + tpLevels[i].ToString("F2") + " | Qty=" + tpQuantities[i]);
					ExitShort(tpQuantities[i], signal, "ShortEntry");
					tpHit[i] = true;

					if (i == 1 && !beActive)
					{
						beActive = true;
						Print(Time[0] + " |   BE ACTIVE | Level=" + entryPrice.ToString("F2"));
					}
					break; // 1 seul TP par barre
				}
			}

			// 2. Verifier le BE (seulement si actif et position ouverte)
			if (beActive && Position.MarketPosition != MarketPosition.Flat)
			{
				if (currentDirection == 1 && Close[0] <= entryPrice)
				{
					Print(Time[0] + " | BE HIT (LONG) | Level=" + entryPrice.ToString("F2"));
					ExitLong("BE", "LongEntry");
					beActive = false;
					currentDirection = 0;
					statBEHits++;
				}
				else if (currentDirection == -1 && Close[0] >= entryPrice)
				{
					Print(Time[0] + " | BE HIT (SHORT) | Level=" + entryPrice.ToString("F2"));
					ExitShort("BE", "ShortEntry");
					beActive = false;
					currentDirection = 0;
					statBEHits++;
				}
			}
		}

		#endregion

		#region Stats

		private void PrintStats()
		{
			var all		= SystemPerformance.AllTrades;
			var longs	= SystemPerformance.LongTrades;
			var shorts	= SystemPerformance.ShortTrades;

			double totalPnL		= all.TradesPerformance.Currency.CumProfit;
			double longPnL		= longs.TradesPerformance.Currency.CumProfit;
			double shortPnL		= shorts.TradesPerformance.Currency.CumProfit;
			int totalCount		= all.Count;
			int longCount		= longs.Count;
			int shortCount		= shorts.Count;
			int longWins		= longs.WinningTrades.Count;
			int shortWins		= shorts.WinningTrades.Count;
			int totalWins		= all.WinningTrades.Count;
			double avgWin		= totalWins > 0 ? all.WinningTrades.TradesPerformance.Currency.CumProfit / totalWins : 0;
			int totalLosses		= all.LosingTrades.Count;
			double avgLoss		= totalLosses > 0 ? all.LosingTrades.TradesPerformance.Currency.CumProfit / totalLosses : 0;

			// Helper qui fait Print + AppendLine dans un StringBuilder, pour sauver tout l'output dans un fichier
			var sb = new System.Text.StringBuilder();
			Action<string> Log = (string s) => { Print(s); sb.AppendLine(s); };

			Log("========================================");
			Log("       MOMENTUM FVG - STATS FINALES     ");
			Log("========================================");
			Log("");
			Log("--- COMPTEURS ---");
			Log("Total trades       : " + totalCount);
			Log("Switches           : " + statSwitchCount);
			Log("Switches bloques   : " + statSwitchBlocked);
			Log("Invalidations FVG  : " + statInvalidations);
			Log("FVG detectees      : " + fvgCount);
			Log("");
			Log("--- LONG ---");
			Log("Trades Long        : " + longCount);
			Log("Wins Long          : " + longWins);
			Log("Losses Long        : " + (longCount - longWins));
			Log("Winrate Long       : " + (longCount > 0 ? (100.0 * longWins / longCount).ToString("F1") : "0") + "%");
			Log("PnL Long           : " + longPnL.ToString("F2") + " $");
			Log("");
			Log("--- SHORT ---");
			Log("Trades Short       : " + shortCount);
			Log("Wins Short         : " + shortWins);
			Log("Losses Short       : " + (shortCount - shortWins));
			Log("Winrate Short      : " + (shortCount > 0 ? (100.0 * shortWins / shortCount).ToString("F1") : "0") + "%");
			Log("PnL Short          : " + shortPnL.ToString("F2") + " $");
			Log("");
			if (EnableTPs)
			{
				Log("--- TAKE PROFITS ---");
				for (int i = 0; i < 4; i++)
					Log("TP" + (i + 1) + " touches       : " + statTPHits[i]);
				Log("BE touches         : " + statBEHits);
			}
			else
				Log("--- TPs DESACTIVES (trailing FVG) ---");
			Log("");
			Log("--- GLOBAL ---");
			Log("PnL Total          : " + totalPnL.ToString("F2") + " $");
			Log("Winrate Global     : " + (totalCount > 0 ? (100.0 * totalWins / totalCount).ToString("F1") : "0") + "%");
			Log("Avg Win            : " + avgWin.ToString("F2") + " $");
			Log("Avg Loss           : " + avgLoss.ToString("F2") + " $");
			Log("Profit Factor      : " + (totalLosses > 0 && avgLoss != 0 ? (all.WinningTrades.TradesPerformance.Currency.CumProfit / Math.Abs(all.LosingTrades.TradesPerformance.Currency.CumProfit)).ToString("F2") : "N/A"));

			// Drawdown
			double maxDD = 0;
			double peak = 0;
			double cumPnL = 0;
			DateTime ddStart = DateTime.MinValue, ddEnd = DateTime.MinValue;
			DateTime currentDDStart = DateTime.MinValue;
			int maxUnderwaterDays = 0;
			DateTime uwStart = DateTime.MinValue;

			for (int t = 0; t < all.Count; t++)
			{
				cumPnL += all[t].ProfitCurrency;
				if (cumPnL > peak)
				{
					peak = cumPnL;
					currentDDStart = all[t].Exit.Time;
					if (uwStart != DateTime.MinValue)
					{
						int uwDays = (int)(all[t].Exit.Time - uwStart).TotalDays;
						if (uwDays > maxUnderwaterDays) { maxUnderwaterDays = uwDays; }
					}
					uwStart = DateTime.MinValue;
				}
				else
				{
					if (uwStart == DateTime.MinValue) uwStart = all[t].Exit.Time;
					double dd = peak - cumPnL;
					if (dd > maxDD)
					{
						maxDD = dd;
						ddStart = currentDDStart;
						ddEnd = all[t].Exit.Time;
					}
				}
			}
			if (uwStart != DateTime.MinValue && all.Count > 0)
			{
				int uwDays = (int)(all[all.Count - 1].Exit.Time - uwStart).TotalDays;
				if (uwDays > maxUnderwaterDays) maxUnderwaterDays = uwDays;
			}

			Log("");
			Log("--- DRAWDOWN ---");
			Log("Max Drawdown       : " + maxDD.ToString("F2") + " $");
			Log("Max Time Underwater: " + maxUnderwaterDays + " jours (" + (maxUnderwaterDays / 30.0).ToString("F1") + " mois)");
			if (ddStart != DateTime.MinValue)
				Log("  Periode          : " + ddStart.ToString("dd/MM/yyyy") + " -> " + ddEnd.ToString("dd/MM/yyyy"));

			// Mois perdants consecutifs
			var monthlyPnL = new SortedDictionary<string, double>();
			for (int t = 0; t < all.Count; t++)
			{
				string key = all[t].Exit.Time.ToString("yyyy-MM");
				if (!monthlyPnL.ContainsKey(key)) monthlyPnL[key] = 0;
				monthlyPnL[key] += all[t].ProfitCurrency;
			}

			int maxConsecLoss = 0, curConsecLoss = 0;
			string consecStart = "", consecEnd = "", curStart = "";
			foreach (var kv in monthlyPnL)
			{
				if (kv.Value < 0)
				{
					if (curConsecLoss == 0) curStart = kv.Key;
					curConsecLoss++;
					if (curConsecLoss > maxConsecLoss)
					{
						maxConsecLoss = curConsecLoss;
						consecStart = curStart;
						consecEnd = kv.Key;
					}
				}
				else
					curConsecLoss = 0;
			}
			Log("Mois perdants consec: " + maxConsecLoss + " mois");
			if (maxConsecLoss > 0)
				Log("  Periode          : " + consecStart + " -> " + consecEnd);

			// PnL mensuel
			Log("");
			Log("--- PNL MENSUEL ---");
			foreach (var kv in monthlyPnL)
			{
				string marker = kv.Value < 0 ? " ***" : "";
				Log("  " + kv.Key + " : " + kv.Value.ToString("F2") + " $" + marker);
			}

			// PnL hebdomadaire
			var weeklyPnL = new SortedDictionary<string, double>();
			var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
			for (int t = 0; t < all.Count; t++)
			{
				DateTime d = all[t].Exit.Time;
				int week = cal.GetWeekOfYear(d, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
				string key = d.Year + "-W" + week.ToString("D2");
				if (!weeklyPnL.ContainsKey(key)) weeklyPnL[key] = 0;
				weeklyPnL[key] += all[t].ProfitCurrency;
			}

			Log("");
			Log("--- PNL HEBDO ---");
			foreach (var kv in weeklyPnL)
			{
				string marker = kv.Value < 0 ? " ***" : "";
				Log("  " + kv.Key + " : " + kv.Value.ToString("F2") + " $" + marker);
			}

			// PRIX ETH HEBDO (pour reconstituer le B&H dans la chart)
			Log("");
			Log("--- ETH WEEKLY PRICES ---");
			foreach (var kv in ethWeeklyPrice)
			{
				Log("  " + kv.Key + " : " + kv.Value.ToString("F2"));
			}

			Log("========================================");

			// ==> VERSION LOG v2 (Path.Combine + UserDataDir) <==
			// Ecrire l'output dans le dossier NinjaTrader (partage Parallels + Mac)
			Print("[LOG v2] UserDataDir = " + NinjaTrader.Core.Globals.UserDataDir);
			try
			{
				string logPath = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "last_backtest_ETHMomentumFVG.log");
				System.IO.File.WriteAllText(logPath, sb.ToString());
				Print("[LOG v2] Stats ecrites dans : " + logPath);
			}
			catch (Exception ex)
			{
				Print("[LOG v2] Echec ecriture : " + ex.Message);
			}
		}

		#endregion

		#region Properties

		[NinjaScriptProperty]
		[Range(1, 50)]
		[Display(Name = "Pivot Length", Description = "Nombre de barres de chaque cote pour confirmer un pivot (defaut: 5)",
			Order = 1, GroupName = "2. Take Profits")]
		public int PivotLength { get; set; }

		[NinjaScriptProperty]
		[Range(0, 50)]
		[Display(Name = "Min TP Distance (%)", Description = "Ecart minimum entre chaque TP en % (defaut: 0.5)",
			Order = 2, GroupName = "2. Take Profits")]
		public double MinTPDistPct { get; set; }

		[NinjaScriptProperty]
		[Range(0.1, 50)]
		[Display(Name = "Discovery (%)", Description = "Si pas de pivot trouve, projeter +/- ce % (defaut: 1.0)",
			Order = 3, GroupName = "2. Take Profits")]
		public double DiscoveryPct { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "TP1 % Position", Description = "% de la position a fermer au TP1 (0 = desactive)",
			Order = 4, GroupName = "2. Take Profits")]
		public double TP1Pct { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "TP2 % Position", Description = "% de la position a fermer au TP2 (0 = desactive)",
			Order = 5, GroupName = "2. Take Profits")]
		public double TP2Pct { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "TP3 % Position", Description = "% de la position a fermer au TP3 (0 = desactive)",
			Order = 6, GroupName = "2. Take Profits")]
		public double TP3Pct { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "TP4 % Position", Description = "% de la position a fermer au TP4 (0 = desactive)",
			Order = 7, GroupName = "2. Take Profits")]
		public double TP4Pct { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Lundi", Order = 1, GroupName = "1.2 Jours de Trading")]
		public bool TradeLundi { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Mardi", Order = 2, GroupName = "1.2 Jours de Trading")]
		public bool TradeMardi { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Mercredi", Order = 3, GroupName = "1.2 Jours de Trading")]
		public bool TradeMercredi { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Jeudi", Order = 4, GroupName = "1.2 Jours de Trading")]
		public bool TradeJeudi { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Vendredi", Order = 5, GroupName = "1.2 Jours de Trading")]
		public bool TradeVendredi { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Samedi", Order = 6, GroupName = "1.2 Jours de Trading")]
		public bool TradeSamedi { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Dimanche", Order = 7, GroupName = "1.2 Jours de Trading")]
		public bool TradeDimanche { get; set; }

		[NinjaScriptProperty]
		[Range(0, 50)]
		[Display(Name = "ADX Minimum", Description = "Seuil ADX minimum pour autoriser un trade (0 = desactive). ADX < 20 = range/chop.",
			Order = 1, GroupName = "1.4 Filtre ADX")]
		public int ADXMinimum { get; set; }

		[NinjaScriptProperty]
		[Range(5, 50)]
		[Display(Name = "Periode ADX", Description = "Periode de l'ADX (defaut: 14).",
			Order = 2, GroupName = "1.4 Filtre ADX")]
		public int ADXPeriod { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Activer Filtre EMA", Description = "Pas de Long sous EMA, pas de Short au-dessus EMA.",
			Order = 1, GroupName = "1.3 Filtre Tendance")]
		public bool EnableEMAFilter { get; set; }

		[NinjaScriptProperty]
		[Range(10, 500)]
		[Display(Name = "Periode EMA", Description = "Periode de l'EMA pour le filtre de tendance (defaut: 160).",
			Order = 2, GroupName = "1.3 Filtre Tendance")]
		public int EMAPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(0, 168)]
		[Display(Name = "Duree Min Trade (h)", Description = "Duree minimum d'un trade avant d'autoriser un switch (0 = desactive). Anti-whipsaw.",
			Order = 1, GroupName = "1.5 Anti-Whipsaw")]
		public int MinTradeDurationH { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Activer TPs", Description = "Active les Take Profits partiels + BE. Si desactive, les positions sont gerees uniquement par les FVG (trailing naturel).",
			Order = 0, GroupName = "2. Take Profits")]
		public bool EnableTPs { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100000)]
		[Display(Name = "Taille Min FVG ($)", Description = "Taille minimum d'une FVG en dollars pour etre prise en compte (0 = pas de filtre)",
			Order = 1, GroupName = "3. FVG Filter")]
		public double MinFVGSize { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name = "Opacite FVG (%)", Description = "Opacite du remplissage des boxes FVG",
			Order = 1, GroupName = "4. Visuel")]
		public int FvgOpacity { get; set; }

		#endregion
	}
}
