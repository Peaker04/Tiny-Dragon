using System.Collections.Generic;
using UnityEngine;

public sealed class ComponentPool<T> where T : Component
{
    private readonly Queue<T> inactiveItems = new Queue<T>();
    private readonly T prefab;
    private readonly Transform parent;

    public ComponentPool(T prefab, Transform parent = null, int prewarmCount = 0)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < prewarmCount; i++)
        {
            T item = CreateItem();
            Release(item);
        }
    }

    public T Get(Vector3 position, Quaternion rotation)
    {
        T item = inactiveItems.Count > 0 ? inactiveItems.Dequeue() : CreateItem();
        item.transform.SetPositionAndRotation(position, rotation);
        item.gameObject.SetActive(true);
        return item;
    }

    public void Release(T item)
    {
        if (item == null)
        {
            return;
        }

        item.gameObject.SetActive(false);
        item.transform.SetParent(parent, false);
        inactiveItems.Enqueue(item);
    }

    private T CreateItem()
    {
        T item = Object.Instantiate(prefab, parent);
        item.gameObject.SetActive(false);
        return item;
    }
}
