# What are Coroutines in Unity and how do they work?
([originally posted on UA / Discussions]([http://answers.unity.com/answers/1749714/view.html](https://discussions.unity.com/t/coroutines-ienumerator-not-working-as-expected/237287/3)))

Even though I have explained what a coroutine actually is several times in many questions, it seems that there is still quite a misunderstanding what a coroutine actually is and how they work.


Lets just step back for a moment. Unity implements coroutines by utilizing a C# feature called "iterator blocks" or iterator methods (which are more generally known as [Generator methods][1]). C# ships with two related interfaces (IEnumerable and IEnumerator) as well as the special keyword "yield".

## IEnumerable / IEnumerable&lt;T&gt;
An IEnumerable simply represents an object that can be iterated and can produce a sequence of "values". This interface is extremely simple and only defines a single method: `GetEnumerator()`. Calling this method creates a new instance of an iterator object that actually implements the sequence. IEnumerable can be seen as a "wrapper" for the actual IEnumerator. The main benefit is that the IEnumerable can essentially store the originally passed parameters / arguments internally and you can produce as many iterator objects you want without the need of specifying the same parameters again.

Note that the difference between the generic version and the "normal" non-generic version is that, as we will see in the next section, the actual "values" we are getting back are either just of type System.Object / object or that we actually get a specific type-safe collection of values

## IEnumerator / IEnumerator&lt;T&gt;
This is the actual heart of the whole deal. This interface provides two main things that can be used to "iterate" over the sequence we are interested in. The only relevant things are: the parameterless method `MoveNext()` as well as the readonly property `Current`. That's all.

The method MoveNext has a return type of `bool` which indicates if it successfully moved to the next item in the collection. The Current property can be used to "read" the current value / item. As mentioned above this is where the generic type parameter comes into play. In the normal / non generic interface the type of this property is just "object" while in the generic version it has the type `T`.

Just for completeness the IEnumerator interface also implements a parameterless method called `Reset()` which is meant to "reset" the internal state of an iterator. What that exactly means is not really specified and I haven't really come across any usage of iterator blocks where this method is actually used. Also auto generated iterators that has been created through the use of the yield keyword do never implement this method because the compiler can never really determine what a reasonable reset would look like.

##Usual usage of IEnumerables / IEnumerators
As we already mentioned those generally represent things you can "iterate over". If C# we have the `foreach` loop which is somewhat related to those interfaces. When you have an object that implements the "IEnumerable" interface you can just use it in a foreach loop like this:
```csharp
    foreach(MyType val in myObject)
    {
        // do something with "val".
    }
```

This actually translates to something like this:

```csharp
    var enumerator = myObject.GetEnumerator();
    while (enumerator.MoveNext())
    {
        MyType val = (MyType)enumerator.Current;
        // do something with "val".
    }
```

(Note I simplified it a little bit. A foreach in addition ensures that Dispose is called if the object implements IDisposable, but that's irrelevant for now).

The key here is that our original object provides a method (GetEnumerator) to **create a new** iterator object. This iterator object is used by the compiler to actively "iterate" through that iterator object by calling MoveNext until it returns false which indicates the end of the collection. After each call to MoveNext the Current property should return the "current value".

## The yield keyword
This is where the compiler magic comes into play. Of course we can simply create our own classes / types which implement the IEnumerable / IEnumerator interfaces and implement our own logic for the MoveNext method. However the yield keyword is a powerful piece of compiler magic. Whenever you use the yield keyword inside a method a drastic change happens. First of all the method must have either IEnumerator or IEnumerable (or the generic equivalents) as return type. Any other return type is not accepted. Further more the compiler will generate a hidden internal class for you that will actually implement your "code".

I try to give you an idea what is happening. Imagine a simple example like this:

```csharp
    IEnumerable<int> MyIterator(int aFrom, int aTo)
    {
        for(int i = aFrom; i < aTo, i++)
            yield return i;
    }
```

This harmless looking "method" will turn into two classes and a method which will look something like this:

```csharp
    IEnumerable<int> MyIterator(int aFrom, int aTo)
    {
        return new ___MyIterator_Enumerable(aFrom, aTo);
    }
    private class ___MyIterator_Enumerable : IEnumerable<int>
    {
        int m_aFrom;
        int m_aTo;
        public ___MyIterator_Enumerable(aFrom, aTo)
        {
            m_aFrom = aFrom;
            m_aTo = aTo;
        }
        public IEnumerator<int> GetEnumerator()
        {
            return new ___MyIterator_Enumerator(m_aFrom, m_aTo);
        }
    }
    
    private class ___MyIterator_Enumerator : IEnumerator<int>
    {
        int m_aFrom;
        int m_aTo;
        int m_State;
        int m_Current;
        int m_i;
        public ___MyIterator_Enumerator(aFrom, aTo)
        {
            m_aFrom = aFrom;
            m_aTo = aTo;
            m_State = -1;
        }
        public int Current
        {
            get {
                if (m_State != 0)
                    throw new SomeException();
                return m_Current;
            }
        }
        public bool MoveNext()
        {
            switch(m_State)
            {
                case -1:
                    m_State = 0;
                    m_i = m_aFrom;
                    if (m_i < m_aTo) {
                        m_Current = m_i;
                        m_State = 0;
                        return true;
                    } else {
                        m_State = 1;
                        return false;
                    }
                break;
                case 0:
                    m_i++;
                    if (m_i < m_aTo) {
                        m_Current = m_i;
                        return true;
                    } else {
                        m_State = 1;
                        return false;
                    }
                break;
                case 1:
                default:
                return false;
            }
        }
        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
```


This is a quite drastic change. Our original method does not contain any of the code we have actually typed in the body. Instead the body of our method has been torn apart and turned into a statemachine object. This generated class is hidden. You won't see this code anywhere unless you decompile your code into IL then you will actually find that internal class.

What's important to realise is that our method does no longer execute the code we've written but just creates an instance of a class. So it creates an instance of that iterator class. Note I specifically made an IEnumerable example just for completeness. If you specify "IEnumerator" as return type that IEnumerable wrapper class just doesn't exist and our method would directly return the IEnumerator class instance instead.

## How Unity uses iterators to implement coroutines

Iterators or generators are mainly meant to iterate over / "generate" values of some kind. However since the C# compiler can magically turn our linear code into a statemachine that splits our code apart at the points where we yield a value, we can actually use that "method to statemachine" magic to implement coroutines.

While in "normal" iterators we are usually interested in the values that are produced by the iterator, we as developers are more interested in the side effects that is in between those yield instructions. When you "call" your coroutine method you don't call any of your code. Keep in mind that all you do is creating a new instance of that statemachine object that the compiler generated for your method. You now pass this object instance to the StartCoroutine method. This does two things at once:

First Unity will create and instance of it's internal "Coroutine" class to managed your coroutine internally and store that iterator object that you passed to StartCoroutine alongside for later use. In addition it will immediately call MoveNext once on that object. This "advance" your statemachine to the next yield statement. The scheduler then will look at the value that you yielded by examin the Current property. Based on that value the scheduler decides what to do next. We don't know the exact implementation of Unity's scheduler since it's implemented in native C++ code. However it most likely will just add that coroutine to a certain "waiting queue". So when you yield "null" the scheduler probably just adds the coroutine to a list of coroutines that should be resumed the next frame. When you yield "WaitForEndOfFrame" it just stores that coroutine in a list that gets processed at the end of the frame.

Each time a coroutine is "resumed" the scheduler simply calls MoveNext and again checks the yielded value when the coroutine wants to be resumed. Of course as soon as MoveNext returns false the coroutine / iterator has finished (either reached the end or a `yield break;` was reached).


## Nested coroutines in Unity

In the latest version of Unity you can actually yield another IEnumerator directly in order to run / iterate that coroutine as subroutine. In the past you could only yield the Coroutine object that StartCoroutine returns. So you had to use StartCoroutine to start the sub coroutine and have the outer coroutine wait for the completion of the inner coroutine.

```csharp
    IEnumerator InnerCoroutine()
    {
        yield return new WaitForSeconds(5);
    }
    IEnumerator OuterRoutine()
    {
        // Something
        yield return StartCoroutine(InnerCoroutine());
        // Something else
    }
```


The old way had several disadvantages. First of all, the outer coroutine needs to have access to the/a MonoBehaviour instance in order to call StartCoroutine for the sub routine. This limits the usage since coroutines can essentially be declared in any class. Also each call to StartCoroutine registers the passed IEnumerator object as a new coroutine to the scheduler, so additional garbage. Also there was a bug when you stop the inner coroutine, the outer coroutine would just hang forever as it waits for the completion of the inner one and that will never happen. Also stopping the outer coroutine did not stop the inner / nested sub coroutine as it is a completely seperate coroutine.

So in more recent Unity versions you can directly do this:

```csharp
    IEnumerator OuterRoutine()
    {
        // Something
        yield return InnerCoroutine();
        // Something else
    }
```

In the past this would not run the inner coroutine and the outer one would just wait for one frame, as the scheduler did not care about IEnumerator yield valies and just treated them like any unknown object and simply waited one frame.

Though in recent Unity versions yielding a sub IEnumerator like this would actually run the inner coroutine as a sub routine inside the same coroutine. We don't know how this is implemented exactly, but if I would implement such a feature, I would simply give each Coroutine object (that internal object that represents a coroutine in the engine) a Stack of IEnumerators. So when the outer coroutine is started, it would be the only instance in that stack. The scheduler would simply iterate the top element on the stack. If the scheduler gets an IEnumerator value from the current iteration, it simply pushes this IEnumerator onto the stack. So now the same coroutine would actually iterate the sub routine until it finishes. Once the top IEnumerator has finished, it is popped from the stack and the outer coroutine is on top again and is continued automatically.

This allows arbitrary nesting of coroutines within a single actual Coroutine instance. This is much more resource friendly. Also when you call StopCoroutine on the outer coroutine object (which is the only actual coroutine), all nested coroutines would be terminated as well.

## Custom example coroutine scheduler
I've written a [small example coroutine scheduler](/CoroutineExample/CoroutineScheduler.cs) that has no dependencies with Unity. It should illustrate how Unity may drive your coroutines. There's an [example MonoBehaviour component over here](/CoroutineExample/CustomCoroutineTest.cs) that shows how you can recreate the default queue, the WaitForSeconds queue as well as the WaitForFixedUpdate queue. There's also an example coroutine that should demonstrate all those features.


  [1]: https://en.wikipedia.org/wiki/Generator_(computer_programming)

