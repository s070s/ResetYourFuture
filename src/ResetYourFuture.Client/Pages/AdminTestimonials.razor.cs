using Microsoft.AspNetCore.Components;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Client.Pages;

public partial class AdminTestimonials
{
    [Inject] private IAdminTestimonialConsumer TestimonialConsumer { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private PagedResult<AdminTestimonialDto>? pagedResult;
    private int currentPage = 1;
    private int pageSize = 10;
    private static readonly int[] PageSizeOptions = [10, 25, 50];
    private string message = string.Empty;
    private Guid? confirmDeleteId;

    protected override async Task OnInitializedAsync()
    {
        await LoadTestimonials();
    }

    private async Task LoadTestimonials()
    {
        pagedResult = await TestimonialConsumer.GetAllAsync( currentPage, pageSize );
    }

    private async Task OnPageSizeChanged( int size )
    {
        pageSize = size;
        currentPage = 1;
        await LoadTestimonials();
    }

    private async Task PreviousPage()
    {
        if ( currentPage > 1 )
        {
            currentPage--;
            await LoadTestimonials();
        }
    }

    private async Task NextPage()
    {
        if ( pagedResult is { HasNextPage: true } )
        {
            currentPage++;
            await LoadTestimonials();
        }
    }

    private void NewTestimonial() => Navigation.NavigateTo( "/admin/testimonials/new" );

    private void EditTestimonial( Guid id ) => Navigation.NavigateTo( $"/admin/testimonials/{id}" );

    private async Task ToggleActive( Guid id )
    {
        try
        {
            var result = await TestimonialConsumer.ToggleActiveAsync( id );
            if ( result is not null )
            {
                message = result.IsActive ? "Testimonial activated." : "Testimonial deactivated.";
                await LoadTestimonials();
            }
            else
            {
                message = "Failed to update testimonial status.";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task MoveUp( Guid id )
    {
        try
        {
            await TestimonialConsumer.MoveUpAsync( id );
            await LoadTestimonials();
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task MoveDown( Guid id )
    {
        try
        {
            await TestimonialConsumer.MoveDownAsync( id );
            await LoadTestimonials();
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }

    private async Task DeleteTestimonial( Guid id )
    {
        try
        {
            var success = await TestimonialConsumer.DeleteAsync( id );
            if ( success )
            {
                confirmDeleteId = null;
                message = "Testimonial deleted.";
                await LoadTestimonials();
            }
            else
            {
                message = "Error deleting testimonial.";
            }
        }
        catch ( Exception ex )
        {
            message = $"Error: {ex.Message}";
        }
    }
}
