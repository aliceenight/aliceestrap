using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace Aliceestrap
{
    public class RemoteDataManager : JsonManager<RemoteDataBase>
    {
        public override string ClassName => nameof(RemoteDataManager);

        public override string LOG_IDENT_CLASS => ClassName;

        public override string FileLocation => Path.Combine(Paths.Base, "Data.json");

        public bool Changed => !OriginalProp.Equals(Prop);

        public GenericTriState LoadedState = GenericTriState.Unknown;

        public event EventHandler DataLoaded = null!;

        public void Subscribe(EventHandler Handler)
        {
            switch (LoadedState)
            {
                case GenericTriState.Unknown:
                    DataLoaded += Handler;
                    break;
                case GenericTriState.Successful:
                    Handler(this, EventArgs.Empty);
                    break;
                default:
                    Handler(this, EventArgs.Empty);
                    break;
            }
        }

        public async Task WaitUntilDataFetched()
        {
            const int delay = 100;
            const int maxTries = 30;
            int tries = 0;

            while (LoadedState == GenericTriState.Unknown)
            {
                await Task.Delay(delay);
                tries++;

                if (tries >= maxTries)
                    break;
            }
        }

        public async Task LoadData()
        {
            const string LOG_IDENT = $"{nameof(RemoteDataManager)}::LoadData";
            App.Logger.WriteLine(LOG_IDENT, "Loading local data");
            this.Load(false);

            LoadedState = GenericTriState.Successful;

            await Task.CompletedTask;

            DataLoaded?.Invoke(this, EventArgs.Empty);

            if (LoadedState == GenericTriState.Successful)
                this.Save();

            App.Logger.WriteLine(LOG_IDENT, $"Loading finished with status: {LoadedState}");
        }
    }
}
