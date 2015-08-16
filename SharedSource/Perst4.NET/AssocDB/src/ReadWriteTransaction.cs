namespace Perst.Assoc
{
    using System;
    using System.IO;
    using System.Text;
    using System.Collections;
    using System.Collections.Generic;
    using Perst;
    using Perst.FullText;

    /// <summary>
    /// Read-write transaction.
    /// AssocDB provides MURSIW (multiple readers single writer) isolation model.
    /// It means that only one transaction can update the database at each moment of time, but multiple transactions
    /// can concurrently read it.
    /// 
    /// All access to the database (read or write) should be performed within transaction body.
    /// 
    /// Transaction should be explicitly started by correspondent method of AssocDB and then it has to be either committed, 
    /// either aborted. In any case, it can not be used any more after commit or rollback - you should start another transaction.
    /// </summary>
    public class ReadWriteTransaction : ReadOnlyTransaction
    {
        /// <summary>
        /// Associate new string value with this item. If there are already some associations with this name for this item, then
        /// new value will be appended at the end.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">attribute value</param>
        public void Link(Item item, String name, String value)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(String));
            int id = index.Oid;
            int l = 0, r = item.stringFields.Length;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] <= id)
                { // we want to locate position after all such IDs
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            index.Put(new Key(value.ToLower()), item);
            item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
            item.stringFields = Arrays.Insert(item.stringFields, r, value);
            modify(item);
        }

        /// <summary>
        /// Associate new string value with this item at given position.
        /// This operation is analog of insertion in an array at specified position.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">attribute value</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully inserted or false if it can not be inserted at this position (position is more than one greater 
        /// than "array" size)</returns>
        public bool LinkAt(Item item, String name, String value, int position)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(String));
            int id = index.Oid;
            int l = 0, n = item.stringFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            if (position != 0)
            {
                r += position;
                if (r > n || ((r == n || item.fieldIds[r] != id) && (r == 0 || item.fieldIds[r - 1] != id)))
                {
                    return false;
                }
            }
            index.Put(new Key(value.ToLower()), item);
            item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
            item.stringFields = Arrays.Insert(item.stringFields, r, value);
            modify(item);
            return true;
        }

        /// <summary>
        /// Associate new numeric value with this item. If there are already some associations with this name for this item, then
        /// new value will be appended at the end.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">attribute value</param>
        public void Link(Item item, String name, double value)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(double));
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int l = nStrings, r = l + item.numericFields.Length;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] <= id)
                {  // we want to locate position after all such IDs
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            index.Put(new Key(value), item);
            item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
            item.numericFields = Arrays.Insert(item.numericFields, r - nStrings, value);
            modify(item);
        }

        /// <summary>
        /// Associate new numeric value with this item at given position.
        /// This operation is analog of insertion in an array at specified position.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">attribute value</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully inserted or false if it can not be inserted at this position (position is more than one greater 
        /// than "array" size)</returns>
        public bool LinkAt(Item item, String name, double value, int position)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(double));
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int l = nStrings, n = l + item.numericFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            if (position != 0)
            {
                r += position;
                if (r > n || ((r == n || item.fieldIds[r] != id) && (r == 0 || item.fieldIds[r - 1] != id)))
                {
                    return false;
                }
            }
            index.Put(new Key(value), item);
            item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
            item.numericFields = Arrays.Insert(item.numericFields, r - nStrings, value);
            modify(item);
            return true;
        }

        /// <summary>
        /// Associate several strings value with this item. If there are already some associations with this name for this item, then
        /// new values will be appended at the end.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="values">array with attribute values</param>
        public void Link(Item item, String name, String[] values)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(String));
            int id = index.Oid;
            int l = 0, r = item.stringFields.Length;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] <= id)
                {  // we want to locate position after all such IDs
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            for (int i = 0; i < values.Length; i++)
            {
                index.Put(new Key(values[i].ToLower()), item);
            }
            item.fieldIds = Arrays.Insert(item.fieldIds, r, id, values.Length);
            item.stringFields = Arrays.Insert(item.stringFields, r, values);
            modify(item);
        }

        /// <summary>
        /// Associate several numeric value with this item. If there are already some associations with this name for this item, then
        /// new values will be appended at the end.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="values">array with attribute values</param>
        public void Link(Item item, String name, double[] values)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(double));
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int l = nStrings, n = l + item.numericFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] <= id)
                {  // we want to locate position after all such IDs
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            for (int i = 0; i < values.Length; i++)
            {
                index.Put(new Key(values[i]), item);
            }
            item.fieldIds = Arrays.Insert(item.fieldIds, r, id, values.Length);
            item.numericFields = Arrays.Insert(item.numericFields, r - nStrings, values);
            modify(item);
        }

        /// <summary>
        /// Add relation to another item. Relation can be either embedded inside object (item) - if total number of links from the
        /// item doesn't reach embedded relation threshold, either stores in separate B-Tree index.
        /// AssocDB automatically adds to the target item inverse link - attribute with name "-XXX" where XXX is specified attribute name.
        /// Inverse links are needed for preserving references consistency in case of updates/deletes but them can be also
        /// used by application for traversal between objects. But You should not try to explicitly update or delete inverse reference.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="target">target item</param>
        public void Link(Item item, String name, Item target)
        {
            checkIfActive();
            checkIfNotInverseLink(name);
            addLink(item, name, target);
            addLink(target, "-" + name, item);
        }

        /// <summary>
        /// Add relation to another item. 
        /// This operation is analog of insertion in an array at specified position.
        /// This operation will fail of relations for this item are already stored in external (non-embedded) way.
        /// 
        /// And unlike <code>link(Item item, String name, Item target)</code> method this 
        /// method never cause change if embedded relation representation to external one even if
        /// total number of links from this item exceeds threshold value for embedded relations.
        /// 
        /// AssocDB automatically adds to the target item inverse link - attribute with name "-XXX" where XXX is specified attribute name.
        /// Inverse links are needed for preserving references consistency in case of updates/deletes but them can be also
        /// used by application for traversal between objects. But You should not try to explicitly update or delete inverse reference.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="target">target item</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully inserted or false if it can not be inserted at this position (position is more than one greater 
        /// than "array" size)</returns>
        public bool LinkAt(Item item, String name, Item target, int position)
        {
            checkIfActive();
            checkIfNotInverseLink(name);
            Index index = getIndex(name, typeof(Object));
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int nNumbers = item.numericFields.Length;
            int nFields = item.fieldIds.Length;

            if (item.relations == null)
            {
                return false;
            }
            int l = nStrings + nNumbers, r = nFields;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            if (position != 0)
            {
                r += position;
                if (r > nFields || ((r == nFields || item.fieldIds[r] != id) && (r == 0 || item.fieldIds[r - 1] != id)))
                {
                    return false;
                }
            }
            index.Put(new Key(target), item);
            item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
            item.relations.Insert(r - nStrings - nNumbers, target);
            modify(item);
            addLink(target, "-" + name, item);
            return true;
        }

        /// <summary>
        /// Add relation to another items. Relation can be either embedded inside object (item) - if total number of links from the
        /// item doesn't reach embedded relation threshold, either stores in separate B-Tree index.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="targets">array with related items</param>
        public void Link(Item item, String name, Item[] targets)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(Object));
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int nNumbers = item.numericFields.Length;
            for (int i = 0; i < targets.Length; i++)
            {
                index.Put(new Key(targets[i]), item);
            }
            if (item.relations != null && item.relations.Length + targets.Length > db.embeddedRelationThreshold)
            {
                int j = nStrings + nNumbers;
                for (int i = 0, n = item.relations.Length; i < n; i++)
                {
                    addRelation(item, item.fieldIds[i + j], item.relations.GetRaw(i));
                }
                item.fieldIds = Arrays.Truncate(item.fieldIds, j);
                item.relations = null;
                modify(item);
            }
            if (item.relations != null)
            {
                int nFields = item.fieldIds.Length;
                int l = nStrings + nNumbers, r = nFields;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (item.fieldIds[m] <= id)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                item.fieldIds = Arrays.Insert(item.fieldIds, r, id, targets.Length);
                for (int i = r - nStrings - nNumbers, j = 0; j < targets.Length; j++)
                {
                    item.relations.Insert(i + j, targets[j]);
                }
                modify(item);
            }
            else
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    addRelation(item, id, targets[i]);
                }
                item.fieldNames = null;
            }
            for (int i = 0; i < targets.Length; i++)
            {
                addLink(targets[i], "-" + name, item);
            }
        }

        /// <summary>
        /// Associate new value with this item. If there are already some associations with this name for this item, then
        /// new value will be appended at the end.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="any">attribute value (should be of String, Double/Number or Item type)</param>
        public void Link(Item item, String name, Object any)
        {
            if (any is String)
            {
                Link(item, name, (String)any);
            }
            else if (any is IConvertible)
            {
                Link(item, name, ((IConvertible)any).ToDouble(null));
            }
            else
            {
                Link(item, name, (Item)any);
            }
        }

        /// <summary>
        /// Associate new value with this item at given position.
        /// This operation is analog of insertion in an array at specified position.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="any">attribute value (should be of String, Double/Number or Item type)</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully inserted or false if it can not be inserted at this position (position is more than one greater 
        /// than "array" size)</returns>
        public bool LinkAt(Item item, String name, Object any, int position)
        {
            return (any is String)
                ? LinkAt(item, name, (String)any, position)
                : (any is IConvertible)
                    ? LinkAt(item, name, ((IConvertible)any).ToDouble(null), position)
                    : LinkAt(item, name, (Item)any, position);
        }

        /// <summary>
        /// Associate new name-value pair with this item
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pair">name-value pair</param>
        public void Link(Item item, Pair pair)
        {
            Link(item, pair.Name, pair.Value);
        }

        /// <summary>
        /// Associate new name-value pair with this item at given position
        /// This operation is analog of insertion in an array at specified position.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pair">name-value pair</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully inserted or false if it can not be inserted at this position (position is more than one greater 
        /// than "array" size)</returns>
        public bool LinkAt(Item item, Pair pair, int position)
        {
            return LinkAt(item, pair.Name, pair.Value, position);
        }

        /// <summary>
        /// Associate new name-value pairs with this item
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pairs">array of name-value pair</param>
        public void Link(Item item, Pair[] pairs)
        {
            for (int i = 0; i < pairs.Length; i++)
            {
                Link(item, pairs[i]);
            }
        }

        /// <summary>
        /// Update association with string value: replace old attribute value with new one
        /// If there are no attributes with the given name associated with this item, then this method just adds new association.
        /// If there are more than one associations with the given name, then first of them is updated
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">new attribute value</param>
        public void Update(Item item, String name, String value)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(String));
            int id = index.Oid;
            int l = 0, n = item.stringFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            index.Put(new Key(value.ToLower()), item);
            if (r < n && item.fieldIds[r] == id)
            {
                index.Remove(new Key(item.stringFields[r].ToLower()), item);
                item.stringFields[r] = value;
            }
            else
            {
                item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
                item.stringFields = Arrays.Insert(item.stringFields, r, value);
            }
            modify(item);
        }

        /// <summary>
        /// Update association with string value at the specified position
        /// This operation is analog of replacing an array element
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">new attribute value</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully updated, false if specified position doesn't belong to "array"</returns>
        public bool UpdateAt(Item item, String name, String value, int position)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(String));
            int id = index.Oid;
            int l = 0, n = item.stringFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            r += position;
            if (r < n && item.fieldIds[r] == id)
            {
                index.Remove(new Key(item.stringFields[r].ToLower()), item);
                index.Put(new Key(value.ToLower()), item);
                item.stringFields[r] = value;
                modify(item);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Update association with numeric value: replace old attribute value with new one
        /// If there are no attributes with the given name associated with this item, then this method just adds new association.
        /// If there are more than one associations with the given name, then first of them is updated
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">new attribute value</param>
        public void Update(Item item, String name, double value)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(double));
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int l = nStrings, r = l + item.numericFields.Length, n = r;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            index.Put(new Key(value), item);
            if (r < n && item.fieldIds[r] == id)
            {
                r -= nStrings;
                index.Remove(new Key(item.numericFields[r]), item);
                item.numericFields[r] = value;
            }
            else
            {
                item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
                item.numericFields = Arrays.Insert(item.numericFields, r - nStrings, value);
            }
            modify(item);
        }

        /// <summary>
        /// Update association with numeric value at the specified position
        /// This operation is analog of replacing an array element
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">new attribute value</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully updated, false if specified position doesn't belong to "array"</returns>
        public bool UpdateAt(Item item, String name, double value, int position)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(double));
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int l = nStrings, r = l + item.numericFields.Length, n = r;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            r += position;
            if (r < n && item.fieldIds[r] == id)
            {
                r -= nStrings;
                index.Put(new Key(value), item);
                index.Remove(new Key(item.numericFields[r]), item);
                item.numericFields[r] = value;
                modify(item);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Update association with other item at the specified position
        /// This operation is analog of replacing an array element
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="target">new associated item</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully updated, false if relation is not embedded or specified position doesn't belong to "array"</returns>
        public bool UpdateAt(Item item, String name, Item target, int position)
        {
            checkIfActive();
            Index index = getIndex(name, typeof(Object));
            int id = index.Oid;
            int offs = item.stringFields.Length + item.numericFields.Length;
            if (item.relations == null)
            {
                return false;
            }
            int l = offs, n = item.fieldIds.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            r += position;
            if (r < n && item.fieldIds[r] == id)
            {
                r -= offs;
                index.Put(new Key(target), item);
                removeLink(item, (Item)item.relations[r], id);
                item.relations[r] = target;
                addLink(target, "-" + name, item);
                modify(item);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Update association: replace old attribute value with new one
        /// If there are no attributes with the given name associated with this item, then this method just adds new association.
        /// If there are more than one associations with the given name, then first of them is updated
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="any">new attribute value</param>
        public void Update(Item item, String name, Object any)
        {
            if (any is String)
            {
                Update(item, name, (String)any);
            }
            else
            {
                Update(item, name, ((IConvertible)any).ToDouble(null));
            }
        }

        /// <summary>
        /// Update association at the specified position
        /// This operation is analog of replacing an array element
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="any">new attribute value</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully updated, false if specified position doesn't belong to "array"</returns>
        public bool UpdateAt(Item item, String name, Object any, int position)
        {
            return (any is String)
                ? UpdateAt(item, name, (String)any, position)
                : (any is IConvertible)
                    ? UpdateAt(item, name, ((IConvertible)any).ToDouble(null), position)
                    : UpdateAt(item, name, (Item)any, position);
        }

        /// <summary>
        /// Update association using given name-value pair
        /// If there are no attributes with the given name associated with this item, then this method just adds new association.
        /// If there are more than one associations with the given name, then first of them is updated
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pair">name-value pair</param>
        public void Update(Item item, Pair pair)
        {
            Update(item, pair.Name, pair.Value);
        }

        /// <summary>
        /// Update association using given name-value pair
        /// This operation is analog of replacing an array element
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pair">name-value pair</param>
        /// <param name="position">position at which new value should be inserted</param>
        /// <returns>true if value is successfully updated, false if specified position doesn't belong to "array"</returns>
        public bool UpdateAt(Item item, Pair pair, int position)
        {
            return UpdateAt(item, pair.Name, pair.Value, position);
        }

        /// <summary>
        /// Update association using given array of name-value pairs
        /// If there are no attributes with the given name associated with this item, then this method just adds new associations.
        /// If there are more than one associations with the given name, then first of them is updated
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pairs">array of name-value pairs</param>
        public void Update(Item item, Pair[] pairs)
        {
            for (int i = 0; i < pairs.Length; i++)
            {
                Update(item, pairs[i]);
            }
        }

        /// <summary>
        /// Remove association with string value for this item.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">attribute value</param>
        /// <returns>true if association with such name and value exists for this item, false otherwise</returns>
        public bool Unlink(Item item, String name, String value)
        {
            checkIfActive();
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return false;
            }
            int l = 0, n = item.stringFields.Length, r = n;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            while (r < n && item.fieldIds[r] == id)
            {
                if (item.stringFields[r] == value)
                {
                    item.stringFields = Arrays.Remove(item.stringFields, r);
                    item.fieldIds = Arrays.Remove(item.fieldIds, r);
                    modify(item);
                    Index index = (Index)db.storage.GetObjectByOID(id);
                    index.Remove(new Key(value.ToLower()), item);
                    return true;
                }
                r += 1;
            }
            return false;
        }

        /// <summary>
        /// Remove association with numeric value for this item.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="value">attribute value</param>
        /// <returns>true if association with such name and value exists for this item, false otherwise</returns>
        public bool Unlink(Item item, String name, double value)
        {
            checkIfActive();
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return false;
            }
            int nStrings = item.stringFields.Length;
            int l = nStrings, r = l + item.numericFields.Length, n = r;
            while (l < r)
            {
                int m = (l + r) >> 1;
                if (item.fieldIds[m] < id)
                {
                    l = m + 1;
                }
                else
                {
                    r = m;
                }
            }
            while (r < n && item.fieldIds[r] == id)
            {
                if (item.numericFields[r - nStrings] == value)
                {
                    item.fieldIds = Arrays.Remove(item.fieldIds, r);
                    item.stringFields = Arrays.Remove(item.stringFields, r - nStrings);
                    modify(item);
                    Index index = (Index)db.storage.GetObjectByOID(id);
                    index.Remove(new Key(value), item);
                    return true;
                }
                r += 1;
            }
            return false;
        }

        /// <summary>
        /// Remove association with value for this item.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="any">attribute value</param>
        /// <returns>true if association with such name and value exists for this item, false otherwise</returns>
        public bool Unlink(Item item, String name, Object any)
        {
            return (any is String)
                ? Unlink(item, name, (String)any)
                : (any is IConvertible)
                    ? Unlink(item, name, ((IConvertible)any).ToDouble(null))
                    : Unlink(item, name, (Item)any);
        }

        /// <summary>
        /// Remove association specified by name-value pair for this item.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pair">name-value pair</param>
        /// <returns>true if association with such name and value exists for this item, false otherwise</returns>
        public bool Unlink(Item item, Pair pair)
        {
            return Unlink(item, pair.Name, pair.Value);
        }

        /// <summary>
        /// Remove associations specified by array of name-value pair for this item.
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="pairs">array of name-value pairs</param>
        /// <returns>number of removed associations </returns>
        public int Unlink(Item item, Pair[] pairs)
        {
            int nUnlinked = 0;
            for (int i = 0; i < pairs.Length; i++)
            {
                if (Unlink(item, pairs[i].Name, pairs[i].Value))
                {
                    nUnlinked += 1;
                }
            }
            return nUnlinked;
        }

        /// <summary>
        /// Remove association between two items
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="target">target item</param>
        /// <returns>true if association between this two items exists, false otherwise</returns>
        public bool Unlink(Item item, String name, Item target)
        {
            checkIfActive();
            checkIfNotInverseLink(name);
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return false;
            }
            if (item.relations != null)
            {
                int offs = item.stringFields.Length + item.numericFields.Length;
                int l = offs, n = item.fieldIds.Length, r = n;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (item.fieldIds[m] < id)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                while (r < n && item.fieldIds[r] == id)
                {
                    if (target.Equals(item.relations.GetRaw(r - offs)))
                    {
                        item.relations.Remove(r - offs);
                        item.fieldIds = Arrays.Remove(item.fieldIds, r);
                        modify(item);
                        removeLink(item, target, id);
                        return true;
                    }
                    r += 1;
                }
            }
            else
            {
                if (db.root.relations.Unlink(new Key(((long)item.Oid << 32) | (uint)id), target))
                {
                    removeLink(item, target, id);
                    item.fieldNames = null;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Remove all associations with this name
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <returns>number of removed associations</returns>
        public int Unlink(Item item, String name)
        {
            checkIfActive();
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return 0;
            }
            int nFields = item.fieldIds.Length;
            int nStrings = item.stringFields.Length;
            int nNumbers = item.numericFields.Length;
            int l, r;
            for (l = 0; l < nFields && item.fieldIds[l] != id; l++) ;
            for (r = l; r < nFields && item.fieldIds[r] == id; r++) ;
            if (l < r)
            {
                Index index = (Index)db.storage.GetObjectByOID(id);
                if (l < nStrings)
                {
                    for (int i = l; i < r; i++)
                    {
                        index.Remove(new Key(item.stringFields[i].ToLower()), item);
                    }
                    item.stringFields = Arrays.Remove(item.stringFields, l, r - l);
                }
                else if (l < nStrings + nNumbers)
                {
                    for (int i = l; i < r; i++)
                    {
                        index.Remove(new Key(item.numericFields[i - nStrings]), item);
                    }
                    item.numericFields = Arrays.Remove(item.numericFields, l - nStrings, r - l);
                }
                else
                {
                    checkIfNotInverseLink(name);
                    int j = l - nStrings - nNumbers;
                    for (int i = l; i < r; i++)
                    {
                        removeLink(item, (Item)item.relations[j], id);
                        item.relations.Remove(j);
                    }
                }
                item.fieldIds = Arrays.Remove(item.fieldIds, l, r - l);
                modify(item);
                return r - l;
            }
            checkIfNotInverseLink(name);
            Key key = new Key(((long)item.Oid << 32) | (uint)id);
            int nUnlinked = 0;
            foreach (Item target in db.root.relations.Range(key, key))
            {
                db.root.relations.Remove(key, target);
                removeLink(item, target, id);
                nUnlinked += 1;
            }
            item.fieldNames = null;
            return nUnlinked;
        }

        /// <summary>
        /// Remove all associations with this name at specified position.
        /// This operation is analog to remove of element from an array
        /// </summary>
        /// <param name="item">source item</param>
        /// <param name="name">attribute name (verb in terms of associative database model)</param>
        /// <param name="position">remove position</param>
        /// <returns>true if position belongs to the "array", false otherwise</returns>
        public bool UnlinkAt(Item item, String name, int position)
        {
            checkIfActive();
            int id;
            if (!db.name2id.TryGetValue(name, out id))
            {
                return false;
            }
            int nFields = item.fieldIds.Length;
            int nStrings = item.stringFields.Length;
            int nNumbers = item.numericFields.Length;
            int i;
            for (i = 0; i < nFields && item.fieldIds[i] != id; i++) ;
            i += position;
            if (i < nFields)
            {
                Index index = (Index)db.storage.GetObjectByOID(id);
                item.fieldIds = Arrays.Remove(item.fieldIds, i);
                if (i < nStrings)
                {
                    index.Remove(new Key(item.stringFields[i].ToLower()), item);
                    item.stringFields = Arrays.Remove(item.stringFields, i);
                }
                else if ((i -= nStrings) < nNumbers)
                {
                    index.Remove(new Key(item.numericFields[i]), item);
                    item.numericFields = Arrays.Remove(item.numericFields, i);
                }
                else
                {
                    checkIfNotInverseLink(name);
                    i -= nNumbers;
                    removeLink(item, (Item)item.relations[i], id);
                    item.relations.Remove(i);
                }
                modify(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Create new item.
        /// You can use <code>link</code> method to add association for this item
        /// </summary>
        public Item CreateItem()
        {
            checkIfActive();
            return db.CreateItem();
        }

        /// <summary>
        /// Rename a verb. This methods allows to change attribute name in all items.
        /// As far as object format in AssocDB is completely dynamic, database schema evaluation doesn't require
        /// to support any other changes rather than renaming of attributes.
        /// Renaming may be necessary because of two reasons:
        /// <ol>
        /// <li>name conflict: assume that you have "time" attribute and later you realize that you need to support
        /// two kind of times: creation time and last modification time</li>
        /// <li>type conflict: AssocDB requires that values of attribute in all objects have the same type.
        /// You can not store for example "age" in  one item as number 35 and in other item - as string "5 months".
        /// If it is really needed you have to introduce new attribute and may be rename existed.</li>     
        /// </ol>
        /// Renaming should be performed before any invocation of Item.getAttributeNames() method which can cache old attribute name.
        /// </summary>
        /// <param name="oldName">old attribute name</param>
        /// <param name="newName">new attribute name</param>
        /// <returns>true if rename is successfully performed, false if operation failed either because 
        /// there is no oldName attribute in the database, either because newName attribute already exists</returns>
        public bool Rename(String oldName, String newName)
        {
            checkIfActive();
            int id;
            if (!db.name2id.TryGetValue(oldName, out id) || db.name2id.ContainsKey(newName))
            {
                return false;
            }
            db.name2id.Remove(oldName);
            db.name2id[newName] = id;
            db.id2name[id] = newName;

            Index index = (Index)db.storage.GetObjectByOID(id);
            db.root.attributes.Remove(new Key(oldName), index);
            db.root.attributes.Put(new Key(newName), index);
            return true;
        }

        /// <summary>
        /// Remove item from the database.
        /// This methods destroy all relations between this item and other items
        /// </summary>
        /// <param name="item">removed item</param>
        public void Remove(Item item)
        {
            checkIfActive();
            int nStrings = item.stringFields.Length;
            for (int i = 0; i < nStrings; i++)
            {
                Index index = (Index)db.storage.GetObjectByOID(item.fieldIds[i]);
                index.Remove(new Key(item.stringFields[i].ToLower()), item);
            }
            int nNumbers = item.numericFields.Length;
            for (int i = 0; i < nNumbers; i++)
            {
                Index index = (Index)db.storage.GetObjectByOID(item.fieldIds[i + nStrings]);
                index.Remove(new Key(item.numericFields[i]), item);
            }
            ExcludeFromFullTextIndex(item);

            if (item.relations != null)
            {
                int nFields = item.fieldIds.Length;
                for (int i = nStrings + nNumbers, j = 0; i < nFields; i++, j++)
                {
                    removeLink(item, (Item)item.relations[j], item.fieldIds[i]);
                }
            }
            else
            {
                long oid = item.Oid;
                IDictionaryEnumerator e = db.root.relations.GetDictionaryEnumerator(new Key(oid << 32, true), new Key((oid + 1) << 32, false), IterationOrder.AscentOrder);
                while (e.MoveNext())
                {
                    removeLink(item, (Item)e.Value, (int)(long)e.Key);
                    db.root.relations.Remove(new Key((long)e.Key), e.Value);
                }
            }
            item.Deallocate();
        }

        /// <summary>
        /// Include specified attributes of the item in full text index. 
        /// Text of the item is assumed to be in default database language(set by AssocDB.setLanguage method)
        /// All previous occurrences of the item in the index are removed.
        /// To completely exclude item from the index it is enough to specify empty list of attributes.
        /// </summary>
        /// <param name="item">item included in full text index</param>
        /// <param name="attributeNames">attributes to be included in full text index</param>
        public void IncludeInFullTextIndex(Item item, String[] attributeNames)
        {
            IncludeInFullTextIndex(item, attributeNames, db.language);
        }

        /// <summary>
        /// Include specified attributes of the item in full text index.
        /// All previous occurrences of the item in the index are removed.
        /// To completely exclude item from the index it is enough to specify empty list of attributes.
        /// </summary>
        /// <param name="item">item included in full text index</param>
        /// <param name="language">text language</param>
        /// <param name="attributeNames">attributes to be included in full text index</param>
        public void IncludeInFullTextIndex(Item item, String[] attributeNames, String language)
        {
            if (attributeNames == null || attributeNames.Length == 0)
            {
                ExcludeFromFullTextIndex(item);
            }
            else
            {
                StringBuilder buf = new StringBuilder();
                for (int i = 0; i < attributeNames.Length; i++)
                {
                    if (i != 0)
                    {
                        buf.Append("; ");
                    }
                    Object val = item.GetAttribute(attributeNames[i]);
                    if (val != null)
                    {
                        if (val is String[])
                        {
                            String[] arr = (String[])val;
                            for (int j = 0; j < arr.Length; j++)
                            {
                                if (j != 0)
                                {
                                    buf.Append(", ");
                                }
                                buf.Append(arr[j]);
                            }
                        }
                        else if (val is double[])
                        {
                            double[] arr = (double[])val;
                            for (int j = 0; j < arr.Length; j++)
                            {
                                if (j != 0)
                                {
                                    buf.Append(", ");
                                }
                                buf.Append(arr[j].ToString());
                            }
                        }
                        else
                        {
                            buf.Append(val.ToString());
                        }
                    }
                }
                db.root.fullTextIndex.Add(item, new StringReader(buf.ToString()), language);
            }
        }

        /// <summary>
        /// Include all string attributes of the item in full text index. 
        /// Text of the item is assumed to be in default database language(set by AssocDB.setLanguage method)
        /// </summary>
        /// <param name="item">item included in full text index</param>
        public void IncludeInFullTextIndex(Item item)
        {
            IncludeInFullTextIndex(item, db.language);
        }

        /// <summary>
        /// Include all string attributes of the item in full text index.
        /// </summary>
        /// <param name="item">item included in full text index</param>
        /// <param name="language">text language</param>
        public void IncludeInFullTextIndex(Item item, String language)
        {
            StringBuilder buf = new StringBuilder();
            for (int i = 0, n = item.stringFields.Length; i < n; i++)
            {
                if (i != 0)
                {
                    buf.Append(", ");
                }
                buf.Append(item.stringFields[i]);
            }
            db.root.fullTextIndex.Add(item, new StringReader(buf.ToString()), language);
        }

        /// <summary>
        /// Exclude item from full text index
        /// </summary>
        /// <param name="item">item to be excluded from full text index</param>
        public void ExcludeFromFullTextIndex(Item item)
        {
            db.root.fullTextIndex.Delete(item);
        }

        /// <summary>
        /// Commit this transaction.
        /// It is not possible to use this transaction object after it is committed
        /// </summary>
        public override void Commit()
        {
            db.storage.Commit();
            base.Commit();
        }

        /// <summary>
        /// Rollback this transaction (undo all changes done by this transaction)
        /// It is not possible to use this transaction object after it is rollbacked
        /// </summary>
        public override void Rollback()
        {
            db.storage.Rollback();
            base.Rollback();
        }


        void removeLink(Item source, Item target, int id)
        {
            Index index = (Index)db.storage.GetObjectByOID(id);
            index.Remove(new Key(target), source);
            if (target.relations != null)
            {
                int[] fieldIds = target.fieldIds;
                int nFields = fieldIds.Length;
                int nDeleted = 0;
                for (int i = target.stringFields.Length + target.numericFields.Length, j = 0; i < nFields; i++)
                {
                    if (!source.Equals(target.relations.GetRaw(j)))
                    {
                        target.fieldIds[i - nDeleted] = fieldIds[i];
                        j += 1;
                    }
                    else
                    {
                        index = (Index)db.storage.GetObjectByOID(fieldIds[i]);
                        index.Remove(new Key(source), target);
                        target.relations.Remove(j);
                        nDeleted += 1;
                    }
                }
                if (nDeleted > 0)
                {
                    fieldIds = new int[nFields -= nDeleted];
                    Array.Copy(target.fieldIds, 0, fieldIds, 0, nFields);
                    target.fieldIds = fieldIds;
                    modify(target);
                }
            }
            else
            {
                String name = db.id2name[id];
                int inverseId = db.name2id[name.StartsWith("-") ? name.Substring(1) : ("-" + name)];
                index = (Index)db.storage.GetObjectByOID(inverseId);
                index.Remove(new Key(source), target);
                target.fieldNames = null;
                db.root.relations.Remove(new Key(((long)target.Oid << 32) | (uint)inverseId), source);
            }
        }

        void addRelation(Item item, int id, Object target)
        {
            long oid = ((IPersistent)target).Oid;
            db.root.relations.Put(new Key((oid << 32) | (uint)id), target);
        }

        void addLink(Item item, String name, Item target)
        {
            Index index = getIndex(name, typeof(Object));
            index.Put(new Key(target), item);
            int id = index.Oid;
            int nStrings = item.stringFields.Length;
            int nNumbers = item.numericFields.Length;

            if (item.relations != null && item.relations.Length >= db.embeddedRelationThreshold)
            {
                int[] fieldIds = new int[nStrings + nNumbers];
                Array.Copy(item.fieldIds, 0, fieldIds, 0, nStrings + nNumbers);
                int j = nStrings + nNumbers;
                for (int i = 0, n = item.relations.Length; i < n; i++)
                {
                    addRelation(item, item.fieldIds[i + j], item.relations.GetRaw(i));
                }
                item.fieldIds = fieldIds;
                item.relations = null;
                modify(item);
            }
            if (item.relations != null)
            {
                int l = nStrings + nNumbers, offs = l, r = item.fieldIds.Length;
                while (l < r)
                {
                    int m = (l + r) >> 1;
                    if (item.fieldIds[m] <= id)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        r = m;
                    }
                }
                item.fieldIds = Arrays.Insert(item.fieldIds, r, id);
                item.relations.Insert(r - offs, target);
                modify(item);
            }
            else
            {
                addRelation(item, id, target);
                item.fieldNames = null;
            }
        }

        Index getIndex(String name, Type cls) 
    { 
        Index index;
        int id;
        if (!db.name2id.TryGetValue(name, out id)) { 
            index = db.storage.CreateThickIndex(cls);
            db.root.attributes.Put(name, index);
            id = index.Oid;
            db.name2id[name] = id;
            db.id2name[id] = name;
        } else { 
            index = (Index)db.storage.GetObjectByOID(id);
            if (cls != index.KeyType) {
                throw new InvalidOperationException("Type conflict for keyword " + name + ": " + cls + " vs. " +  index.KeyType);
            }
        }
        return index;
    }

        void checkIfNotInverseLink(String name)
        {
            if (name.StartsWith("-"))
            {
                throw new InvalidOperationException("Inverse links can not be excplitely updated");
            }
        }

        void modify(Item item)
        {
            item.fieldNames = null;
            item.Modify();
        }


        protected internal ReadWriteTransaction(AssocDB db)
            : base(db)
        {
        }
    }
}