type Expr =
  | Number of int
  | Mul of Expr * Expr
  | Div of Expr * Expr

// DEMO: Recursive 'eval' function
// DEMO: Short-circuiting Mul by zero
// DEMO: Recursive 'safeEval' function
// DEMO: Pattern matching on option  

// ((2*2)*10)
let e1 = Mul( Mul(Number(2), Number(2)), Number(10) )
let e2 = Mul(Number(0), e1)

let rec eval e = 
  match e with 
  | Number n -> n
  | Mul(Number(0), _) -> 0    
  | Mul(_, Number(0)) -> 0    
  | Mul(e1, e2) -> 
      eval e1 * eval e2
  | Div(e1, e2) ->     
      eval e1 / eval e2

eval e1
eval e2

let rec safeEval e = 
  match e with 
  | Number n -> Some n
  | Div(e1, e2) ->     
      match safeEval e2 with 
      | None -> None
      | Some 0 -> None
      | Some n2 ->
          match safeEval e1 with 
          | None -> None 
          | Some n1 -> Some(n1 / n2)
  | Mul(e1, e2) -> 
      match safeEval e1, safeEval e2 with 
      | Some n1, Some n2 -> Some(n1 * n2)
      | _ -> None      

let e10 = Mul(Number 2, Div(Number 10, Number 0))
let e11 = Mul(Number 2, Div(Number 10, Number 2))
safeEval e10
safeEval e11

