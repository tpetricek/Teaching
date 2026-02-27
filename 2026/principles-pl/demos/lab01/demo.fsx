// DEMO: Adding apples and oranges
// DEMO: Using System.Random
// DEMO: Win if 6, Lost otherwise
// DEMO: Defining dice function (unit vs. sides)
// DEMO: Let and printing the result

let rnd = 
  System.Random()

let dice sides = 
  rnd.Next(sides) + 1

if dice 3 = 3 then "WIN!!!" else "Lost :-("

// DEMO: 2D point as a tuple
// DEMO: Rotate point, shift by dx, print point

let pt = (10, 5)
let rotate (x, y) = (y, x)
let shiftX dx (x, y) = (x + dx, y)

shiftX 100 (rotate pt)

// DEMO: 2D point as a record
// DEMO: Rotate point, shift by dx, print point

type Point = { X : int; Y : int }

let pt1 = { X = 10; Y = 5 } 

let rotatePoint pt = 
  { X = pt.Y; Y = pt.X } 

let shiftXPoint dx pt = 
  { pt with X = pt.X + dx } 

shiftXPoint 100 (rotatePoint pt1)


// DEMO: Shapes as DU (square, circle)
// DEMO: Implementing area, adding rect

type Shape = 
  | Square of int 
  | Circle of int
  | Rect of int * int

let s1 = Square(100)
let s2 = Circle(50)  
let s3 = Rect(10, 100)

let area shape =
  match shape with 
  | Square(side) -> float (side * side)
  | Circle(radius) -> float (radius * radius) * System.Math.PI
  | Rect(s1, s2) -> float (s1 * s2)

area s1
area s2
area s3















