using System;
using System.Threading.Tasks;

namespace Framework.Caspar
{
    public class Service : Layer.Entity
    {
        public class Layer : global::Framework.Caspar.Layer { }

        public Service(long UID) : base(Api.Singleton<Service.Layer>.Instance)
        {
            this.UID = UID;
        }
        protected async ValueTask Do(Func<Task> job)
        {
            await PostMessage(job);
        }

        protected async ValueTask<T> Do<T>(Func<Task<T>> job)
        {
            return await PostMessage(job);
        }

        // public async Task WaitComplete() {
        //     if (System.Threading.Interlocked.CompareExchange(ref state, (int)State.IDLE, (int)State.IDLE) == (int)State.IDLE)
        //     {
        //         return;
        //     }

        //     await PostMessage(async () => { });
        //     return;
        // }
    }
}