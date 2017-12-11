﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFGLib.Parsers.Forests;
using CFGLib.Actioneer;

namespace CFGLib.Parsers.Earley {
	public class EarleyParser2 : Parser {
		private readonly BaseGrammar _grammar;

		public EarleyParser2(BaseGrammar grammar) {
			_grammar = grammar;
		}

		public override ForestInternal ParseGetForest(Sentence s) {
			var sppf = ParseGetSppf(s);
			//foreach (var family in sppf.Families) {
			//	var str = family.ToString();
			//}
			// throw new NotImplementedException();
			// return new ForestInternal(sppf, sppf.Symbol);
			return null;
		}

		public override double ParseGetProbability(Sentence s) {
			var sppf = ParseGetSppf(s);
			if (sppf == null) {
				return 0.0;
			}
			var oldSppf = OldFromNew(sppf);
			var prob = EarleyParser.GetProbFromSppf(_grammar, oldSppf);
			return prob;
		}

		private SymbolNode OldFromNew(SppfNode2 sppf) {
			throw new NotImplementedException();
		}

		//public TraverseResultCollection Traverse(Sentence input) {
		//	var sppf = ParseGetSppf(input);

		//	var trav = new Traversal(sppf, input, gp);
		//	var resultList = trav.Traverse();
		//}

		// [Sec 5, ES2008]
		// I've taken the liberty of adjusting the indices of a to start from 0
		private SppfNode2 ParseGetSppf(Sentence a) {
			if (a.Count == 0) {
				throw new ArgumentException("Not sure how to handle empty yet");
			}
			var S = _grammar.Start;

			// E_0, ..., E_n, R, Q′, V = ∅
			EarleySet[] E = new EarleySet[a.Count + 1]; // need +1?
			for (var i = 0; i < E.Length; i++) {
				E[i] = new EarleySet();
			}
			EarleySet Q = new EarleySet();
			EarleySet QPrime = new EarleySet();
			EarleySet R = new EarleySet();
			var H = new Dictionary<Nonterminal, SppfNode2>();
			var V = new Dictionary<SppfNode2, SppfNode2>();
			
			// for all (S ::= α) ∈ P {
			foreach (var production in _grammar.ProductionsFrom(S)) {
				var alpha = production.Rhs;
				var potentialItem = new EarleyItem(new DecoratedProduction(production, 0), 0, null);

				// if α ∈ Σ_N, add (S ::= · α, 0, null) to E_0
				if (InSigma(alpha)) {
					E[0].Add(potentialItem);
				}
				// if α = a_0 α′, add (S ::= · α, 0, null) to Q′
				// TODO: not sure how to handle this when a is ε
				else {
					var a1 = a.First();
					if (alpha.First() == a1) {
						QPrime.Add(potentialItem);
					}
				}
			}

			// for 0 ≤ i ≤ n {
			for (var i = 0; i < E.Length; i++) {
				// H = ∅, R = E_i , Q = Q ′
				H = new Dictionary<Nonterminal, SppfNode2>();
				R = new EarleySet(E[i]);
				Q = QPrime;

				// Q′ = ∅
				QPrime = new EarleySet();

				// while R ̸= ∅ {
				while (!R.IsEmpty) {
					// remove an element, Λ say, from R
					var Λ = R.TakeOne();
					var w = Λ.SppfNode;
					var h = Λ.StartPosition;

					// if Λ = (B ::= α · Cβ, h, w) {
					if (Λ.NextWord is Nonterminal C) {
						var β = Λ.Tail;
						// for all (C ::= δ) ∈ P {
						foreach (var production in _grammar.ProductionsFrom(C)) {
							// if δ ∈ Σ_N and (C ::= ·δ, i, null) ̸∈ E_i {
							var δ = production.Rhs;
							var newItem = new EarleyItem(new DecoratedProduction(production, 0), i, null);
							if (InSigma(δ)) {
								// add (C ::= ·δ, i, null) to E_i and R }
								if (!E[i].Contains(newItem)) {
									E[i].Add(newItem);
									R.Add(newItem);
								}
							} else {
								// if δ = a_i δ′ { add (C ::= · δ, i, null) to Q }
								var aCurr = a[i];
								if (δ.First() == aCurr) {
									Q.Add(newItem);
								}
							}
						}

						// if ((C, v) ∈ H) {
						if (H.TryGetValue(C, out SppfNode2 v)) {
							// let y = MAKE_NODE(B ::= αC · β, h, i, w, v, V)
							var productionAdvanced = Λ.DecoratedProduction.Increment();
							var y = MakeNode(productionAdvanced, h, i, w, v, V);

							var newItem = new EarleyItem(productionAdvanced, h, y);
							// if β ∈ Σ N and (B ::= αC · β, h, y) ̸∈ E_i {
							if (InSigma(β)) {
								if (!E[i].Contains(newItem)) {
									// add(B::= αC · β, h, y) to E_i and R }
									E[i].Add(newItem);
									R.Add(newItem);
								}
							} else {
								// if β = a_i β′ { add (B ::= αC · β, h, y) to Q } } }
								var aCurr = a[i];
								if (β.First() == aCurr) {
									Q.Add(newItem);
								}
							}
						}
					}
					// if Λ = (D ::= α·, h, w) {
					else if (Λ.NextWord == null) {
						var D = Λ.DecoratedProduction.Production.Lhs;
						// if w = null {
						if (w == null) {
							// if there is no node v ∈ V labelled (D, i, i) create one
							var potentialV = new SppfNode2(D, i, i);
							if (!V.TryGetValue(potentialV, out SppfNode2 v)) {
								v = potentialV;
								V[potentialV] = potentialV;
							}
							// set w = v
							w = v;
							// if w does not have family (ϵ) add one
							w.AddFamily();
						}

						// if h = i { add (D, w) to H }
						if (h == i) {
							if (H.ContainsKey(D)) {
								throw new Exception();
							}
							H[D] = w;
						}

						// for all (A ::= τ · Dδ, k, z) in E_h {
						for (var itemi = 0; itemi < E[h].Count; itemi++) {
							var item = E[h][itemi];
							if (item.NextWord != D) {
								continue;
							}
							var k = item.StartPosition;
							var z = item.SppfNode;
							var δ = item.Tail;
							// let y = MAKE NODE(A ::= τD · δ, k, i, z, w, V)			
							var productionAdvanced = item.DecoratedProduction.Increment();
							var y = MakeNode(productionAdvanced, k, i, z, w, V);

							var newItem = new EarleyItem(productionAdvanced, k, y);
							// if δ ∈ Σ_N and (A ::= τD · δ, k, y) ̸∈ E_i {
							if (InSigma(δ)) {
								if (!E[i].Contains(newItem)) {
									// add (A ::= τD · δ, k, y) to E_i and R
									E[i].Add(newItem);
									R.Add(newItem);
								}
							} else {
								// if δ = a_i δ′ { add (A ::= τD · δ, k, y) to Q } }
								// TODO: not exactly sure what should happen when a[i] is oob
								//if (i > a.Count) {
								//	throw new Exception();
								//} else if (i == a.Count) {
								//	Q.Add(newItem);
								//} else {

								if (i < a.Count) {
									var aCurr = a[i];
									if (δ.First() == aCurr) {
										Q.Add(newItem);
									}
								}
							}
						}
					} else {
						throw new Exception("Didn't expect a terminal");
					}
				}
				if (i < a.Count) {
					V = new Dictionary<SppfNode2, SppfNode2>();
					// create an SPPF node v labelled(a_i, i, i + 1)
					var v2 = new SppfNode2(a[i], i, i + 1);

					// while Q ̸= ∅ {
					while (!Q.IsEmpty) {
						// remove an element, Λ = (B ::= α · a_i β, h, w) say, from Q
						// TODO: the statement above seems to imply all elements of Q have that form, but this doesn't seem to happen.  Skip them if they don't?
						var Λ = Q.TakeOne();
						if (Λ.NextWord != a[i]) {
							throw new Exception();
							// continue;
						}
						var h = Λ.StartPosition;
						var w = Λ.SppfNode;
						var β = Λ.Tail;

						// let y = MAKE NODE(B ::= α a_i · β, h, i + 1, w, v, V)
						var productionAdvanced = Λ.DecoratedProduction.Increment();
						var y = MakeNode(productionAdvanced, h, i + 1, w, v2, V);

						var newItem = new EarleyItem(productionAdvanced, h, y);
						// if β ∈ Σ_N { add (B ::= α a_i · β, h, y) to E_i+1 }
						if (InSigma(β)) {
							E[i + 1].Add(newItem);
						}

						// if β = a_i+1 β′ { add (B ::= α a_i · β, h, y) to Q′ }
						else {
							var aNext = a[i + 1];
							if (β.First() == aNext) {
								QPrime.Add(newItem);
							}
						}
					}
				}
			}
			// if (S ::= τ ·, 0, w) ∈ E_n return w
			foreach (var item in E[a.Count]) {
				if (item.DecoratedProduction.Production.Lhs != S) {
					continue;
				}
				if (item.StartPosition != 0) {
					continue;
				}
				if (!item.DecoratedProduction.AtEnd) {
					continue;
				}
				return item.SppfNode;
				// TODO: could there be others?
			}
			// else return failure
			return null;
		}

		// MAKE_NODE(B ::= αx · β, j, i, w, v, V) {
		private SppfNode2 MakeNode(DecoratedProduction decoratedProduction, int j, int i, SppfNode2 w, SppfNode2 v, Dictionary<SppfNode2, SppfNode2> V) {
			var β = decoratedProduction.Suffix;
			var α = decoratedProduction.Prefix;

			// hacking in sum type
			Tuple<Word, DecoratedProduction> s;
			// if β = ϵ { let s = B } else { let s = (B ::= αx · β) }
			if (β.Count == 0) {
				s = Tuple.Create<Word, DecoratedProduction>(decoratedProduction.Production.Lhs, null);
			} else {
				s = Tuple.Create<Word, DecoratedProduction>(null, decoratedProduction);
			}

			SppfNode2 y;
			// if α = ϵ and β ̸= ϵ { let y = v }
			if (α.Count == 0 && β.Count != 0) {
				y = v;
			} else {
				// if there is no node y ∈ V labelled (s,j,i) create one and add it to V
				var potentialY = new SppfNode2(s, j, i);
				if (!V.TryGetValue(potentialY, out y)) {
					V[potentialY] = potentialY;
					y = potentialY;
				}

				// if w = null and y does not have a family of children (v) add one
				if (w == null) {
					y.AddFamily(v);
				}
				// if w ̸= null and y does not have a family of children(w, v) add one
				else {
					y.AddFamily(w, v);
				}
			}

			return y;
		}

		private bool InSigma(Sentence alpha) {
			if (alpha.Count == 0) {
				return true;
			}
			if (alpha.First().IsNonterminal) {
				return true;
			}
			return false;
		}
	}
}
