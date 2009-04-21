﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace NMaier.GetOptNet
{
    abstract public partial class GetOpt
    {
        private bool isIList(Type aType)
        {
            if (!aType.IsGenericType)
            {
                return false;
            }
            if (aType.ContainsGenericParameters)
            {
                throw new ProgrammingError("Generic type not closed!");
            }
            Type[] gens = aType.GetGenericArguments();
            if (gens.Length == 1)
            {
                Type genType = typeof(IList<>).MakeGenericType(gens);
                if (aType.GetInterface(genType.Name) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void Initialize()
        {
            Type me = GetType();
            opts = me.GetCustomAttributes(typeof(GetOptOptions), true)[0] as GetOptOptions;
            BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;
            foreach (MemberInfo[] infoArray in new MemberInfo[][] { me.GetFields(flags), me.GetProperties(flags) })
            {
                foreach (MemberInfo info in infoArray)
                {
                    if (info.GetCustomAttributes(typeof(Parameters), true).Length == 1)
                    {
                        if (parameters != null || info.MemberType != MemberTypes.Field)
                        {
                            throw new ProgrammingError("Duplicate declaration for parameters");
                        }
                        FieldInfo field = info as FieldInfo;
                        if (field == null)
                        {
                            throw new ProgrammingError("W00T?");
                        }
                        if (field.FieldType.IsArray)
                        {
                            parameters = new ArrayArgumentHandler(this, field, field.FieldType);
                        }
                        else if (isIList(field.FieldType))
                        {
                            parameters = new IListArgumentHandler(this, field, field.FieldType);
                        }
                        else
                        {
                            throw new ProgrammingError("parameters must be an array type or a IList implementation");
                        }
                        handlers.Add(parameters);
                        continue;
                    }
                    Argument[] args = info.GetCustomAttributes(typeof(Argument), true) as Argument[];
                    if (args.Length != 1)
                    {
                        continue;
                    }
                    if (opts.AcceptPrefix == ArgumentPrefixType.None)
                    {
                        throw new ProgrammingError("You used Prefix=None, hence there are no arguments allowed!");
                    }
                    Argument arg = args[0];
                    string name = arg.GetArg();
                    if (String.IsNullOrEmpty(name))
                    {
                        name = info.Name;
                    }
                    if (longs.ContainsKey(name))
                    {
                        throw new ProgrammingError(String.Format("Duplicate argument {0}", name));
                    }

                    PropertyInfo pi = info as PropertyInfo;
                    FieldInfo fi = info as FieldInfo;
                    ArgumentHandler ai;
                    Type memberType;
                    if (pi != null)
                    {
                        if (!pi.CanWrite)
                        {
                            throw new ProgrammingError(String.Format("Property {0} is an argument but not assignable", info.Name));
                        }
                        memberType = pi.PropertyType;
                    }
                    else if (fi != null)
                    {
                        memberType = fi.FieldType;
                    }
                    else
                    {
                        throw new ProgrammingError("WTF?!");
                    }
                    if (memberType.IsArray)
                    {
                        ai = new ArrayArgumentHandler(this, info, memberType);
                    }
                    else if (isIList(memberType))
                    {
                        ai = new IListArgumentHandler(this, info, memberType);
                    }
                    else
                    {
                        if (memberType == typeof(bool) || memberType == typeof(Boolean) || memberType.IsSubclassOf(typeof(Boolean)))
                        {
                            ai = new FlagArgumentHandler(this, info, arg.OnCollision);
                        }
                        else if (info.GetCustomAttributes(typeof(Counted), true).Length != 0)
                        {
                            ai = new CounterArgumentHandler(this, info, memberType);
                        }
                        else
                        {
                            ai = new PlainArgumentHandler(this, info, memberType, arg.OnCollision);
                        }
                    }

                    longs.Add(name, ai);
                    handlers.Add(ai);

                    foreach (ArgumentAlias alias in info.GetCustomAttributes(typeof(ArgumentAlias), true))
                    {
                        string an = alias.GetAlias();
                        if (longs.ContainsKey(an))
                        {
                            throw new ProgrammingError(String.Format("Duplicate alias argument {0}", an));
                        }
                        longs.Add(an, ai);
                    }
                    foreach (ShortArgument sa in info.GetCustomAttributes(typeof(ShortArgument), true))
                    {
                        char an = sa.GetArg();
                        if (shorts.ContainsKey(an))
                        {
                            throw new ProgrammingError(String.Format("Duplicate short argument {0}", an));
                        }
                        shorts.Add(an, ai);
                    }
                    foreach (ShortArgumentAlias sa in info.GetCustomAttributes(typeof(ShortArgumentAlias), true))
                    {
                        char an = sa.GetAlias();
                        if (shorts.ContainsKey(an))
                        {
                            throw new ProgrammingError(String.Format("Duplicate short argument {0}", an));
                        }
                        shorts.Add(an, ai);
                    }
                }
            }
        }
    }
}