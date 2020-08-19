using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Bencodex.Types;
using Libplanet;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;

namespace Nekoyume.Model.State
{
    /// <summary>
    /// This is a model class of shop state.
    /// </summary>
    [Serializable]
    public class ShopState : State
    {
        public static readonly Address Address = Addresses.Shop;

        private readonly Dictionary<Address, List<ShopItem>> _agentProducts =
            new Dictionary<Address, List<ShopItem>>();

        private readonly Dictionary<Guid, ShopItem> _products = new Dictionary<Guid, ShopItem>();

        public IReadOnlyDictionary<Address, List<ShopItem>> AgentProducts => _agentProducts;

        public ShopState() : base(Address)
        {
        }

        public ShopState(Dictionary serialized)
            : base(serialized)
        {
            _agentProducts = ((Dictionary) serialized["agentProducts"]).ToDictionary(
                kv => kv.Key.ToAddress(),
                kv => ((List) kv.Value)
                    .Select(d => new ShopItem((Dictionary) d))
                    .ToList()
            );

            _products = ((Dictionary) serialized["products"]).ToDictionary(
                kv => kv.Key.ToGuid(),
                kv => new ShopItem((Dictionary) kv.Value));
        }

        public override IValue Serialize() =>
            new Dictionary(new Dictionary<IKey, IValue>
            {
                [(Text) "agentProducts"] = new Dictionary(
                    _agentProducts.Select(kv =>
                        new KeyValuePair<IKey, IValue>(
                            (Binary) kv.Key.Serialize(),
                            new List(kv.Value.Select(i => i.Serialize()))))),
                [(Text) "products"] = new Dictionary(
                    _products.Select(kv =>
                        new KeyValuePair<IKey, IValue>(
                            (Binary) kv.Key.Serialize(),
                            kv.Value.Serialize()))),
            }.Union((Dictionary) base.Serialize()));

        public ShopItem Register(Address sellerAgentAddress, ShopItem shopItem)
        {
            if (!_agentProducts.ContainsKey(sellerAgentAddress))
            {
                _agentProducts.Add(sellerAgentAddress, new List<ShopItem>());
            }

            var shopItems = _agentProducts[sellerAgentAddress];
            if (shopItems.Contains(shopItem))
            {
                throw new ShopStateAlreadyContainsException(
                    $"{nameof(_agentProducts)}, {sellerAgentAddress}, {shopItem.ProductId}");
            }

            shopItems.Add(shopItem);
            _agentProducts[sellerAgentAddress] = shopItems;

            if (_products.ContainsKey(shopItem.ProductId))
            {
                throw new ShopStateAlreadyContainsException(
                    $"{nameof(_products)}, {sellerAgentAddress}, {shopItem.ProductId}");
            }

            _products.Add(shopItem.ProductId, shopItem);

            return shopItem;
        }

        public void Unregister(Address sellerAgentAddress, ShopItem shopItem)
        {
            Unregister(sellerAgentAddress, shopItem.ProductId);
        }

        public void Unregister(Address sellerAgentAddress, Guid productId)
        {
            if (!_agentProducts.ContainsKey(sellerAgentAddress))
            {
                throw new NotFoundInShopStateException(
                    $"{nameof(_agentProducts)}, {sellerAgentAddress}, {productId}");
            }

            var shopItems = _agentProducts[sellerAgentAddress];
            var shopItem = shopItems.FirstOrDefault(item => item.ProductId.Equals(productId));
            if (shopItem is null)
            {
                throw new NotFoundInShopStateException(
                    $"{nameof(_agentProducts)}, {sellerAgentAddress}, {productId}");
            }

            shopItems.Remove(shopItem);
            if (shopItems.Count == 0)
            {
                _agentProducts.Remove(sellerAgentAddress);
            }
            else
            {
                _agentProducts[sellerAgentAddress] = shopItems;
            }

            if (!_products.ContainsKey(shopItem.ProductId))
            {
                throw new NotFoundInShopStateException(
                    $"{nameof(_products)}, {sellerAgentAddress}, {shopItem.ProductId}");
            }

            _products.Remove(shopItem.ProductId);
        }

        public bool TryGet(
            Address sellerAgentAddress,
            Guid productId,
            out KeyValuePair<Address, ShopItem> outPair)
        {
            if (!_agentProducts.ContainsKey(sellerAgentAddress))
            {
                return false;
            }

            var list = _agentProducts[sellerAgentAddress];

            foreach (var shopItem in list.Where(shopItem => shopItem.ProductId == productId))
            {
                outPair = new KeyValuePair<Address, ShopItem>(sellerAgentAddress, shopItem);
                return true;
            }

            return false;
        }

        [Obsolete("Use Unregister()")]
        public bool TryUnregister(
            Address sellerAgentAddress,
            Guid productId,
            out ShopItem outUnregisteredItem)
        {
            if (!TryGet(sellerAgentAddress, productId, out var outPair))
            {
                outUnregisteredItem = null;
                return false;
            }

            _agentProducts[outPair.Key].Remove(outPair.Value);
            _products.Remove(outPair.Value.ProductId);

            outUnregisteredItem = outPair.Value;
            return true;
        }
    }

    [Serializable]
    public class ShopStateAlreadyContainsException : Exception
    {
        public ShopStateAlreadyContainsException(string message) : base(message)
        {
        }

        protected ShopStateAlreadyContainsException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class NotFoundInShopStateException : Exception
    {
        public NotFoundInShopStateException(string message) : base(message)
        {
        }

        protected NotFoundInShopStateException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
